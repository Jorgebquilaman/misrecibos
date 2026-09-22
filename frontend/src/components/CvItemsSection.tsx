import { useState } from 'react';
import { Copy, Download, Eye, Pencil, Plus, Trash2 } from 'lucide-react';
import { cvApi } from '../api';
import type { CvItemDto, SeccionCvItem } from '../types';
import { descargarBlob, formatFecha } from '../utils';

interface Props {
  seccion: SeccionCvItem;
  titulo: string;
  textoAgregar: string;
  categorias: string[] | null; // null => categoría libre (input de texto)
  items: CvItemDto[];
  onCambio: () => void;
  onVer: (blob: Blob, nombre: string) => void;
  onError: (msg: string | null) => void;
  onMensaje: (msg: string | null) => void;
}

interface FormState {
  id?: string;
  categoria: string;
  titulo: string;
  institucion: string;
  descripcion: string;
  fechaDesde: string;
  fechaHasta: string;
  actualidad: boolean;
  archivos: File[];
}

const nuevoForm = (): FormState => ({
  categoria: '',
  titulo: '',
  institucion: '',
  descripcion: '',
  fechaDesde: '',
  fechaHasta: '',
  actualidad: false,
  archivos: []
});

const tamanoLegible = (bytes: number) => {
  if (bytes >= 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  if (bytes >= 1024) return `${Math.round(bytes / 1024)} KB`;
  return `${bytes} B`;
};

export default function CvItemsSection({ seccion, titulo, textoAgregar, categorias, items, onCambio, onVer, onError, onMensaje }: Props) {
  const [form, setForm] = useState<FormState | null>(null);
  const [guardando, setGuardando] = useState(false);
  const [procesando, setProcesando] = useState<string | null>(null);

  const mios = items.filter((x) => x.seccion === seccion);

  const abrirEditar = (x: CvItemDto) => {
    onError(null);
    setForm({
      id: x.id,
      categoria: x.categoria,
      titulo: x.titulo,
      institucion: x.institucion ?? '',
      descripcion: x.descripcion ?? '',
      fechaDesde: x.fechaDesde.slice(0, 10),
      fechaHasta: x.fechaHasta?.slice(0, 10) ?? '',
      actualidad: !x.fechaHasta,
      archivos: []
    });
    window.scrollTo({ top: 0, behavior: 'smooth' });
  };

  const guardar = async () => {
    if (!form) return;
    if (!form.categoria.trim() || !form.titulo.trim() || !form.fechaDesde) {
      onError('Completá categoría, título y fecha de inicio.');
      return;
    }
    setGuardando(true);
    onError(null);
    const data = {
      seccion,
      categoria: form.categoria.trim(),
      titulo: form.titulo.trim(),
      institucion: form.institucion || null,
      descripcion: form.descripcion || null,
      fechaDesde: form.fechaDesde,
      fechaHasta: form.actualidad ? null : (form.fechaHasta || null)
    };
    try {
      let id = form.id;
      if (id) {
        await cvApi.editarItem(id, data);
        onMensaje('Ítem actualizado. Se incluye en tu CV.');
      } else {
        const creado = await cvApi.crearItem(data);
        id = creado.id;
        onMensaje('Ítem agregado. Se incluye en tu CV.');
      }
      if (id && form.archivos.length > 0) {
        for (const f of form.archivos) {
          await cvApi.subirItemAdjunto(id, f);
        }
      }
      setForm(null);
      onCambio();
    } catch (e: any) {
      onError(e.response?.data?.error ?? 'No se pudo guardar el ítem.');
    } finally {
      setGuardando(false);
    }
  };

  const duplicar = async (id: string) => {
    setProcesando(id);
    onError(null);
    try {
      const clon = await cvApi.duplicarItem(id);
      setMensajeTemporal('Ítem duplicado. Ajustá los datos y guardá los cambios.');
      onCambio();
      abrirEditar(clon);
    } catch (e: any) {
      onError(e.response?.data?.error ?? 'No se pudo duplicar el ítem.');
    } finally {
      setProcesando(null);
    }
  };

  const setMensajeTemporal = (m: string) => onMensaje(m);

  const eliminar = async (id: string) => {
    if (!confirm('¿Eliminar este ítem? También se borran sus archivos adjuntos.')) return;
    setProcesando(id);
    onError(null);
    try {
      await cvApi.eliminarItem(id);
      onMensaje('Ítem eliminado.');
      onCambio();
    } catch (e: any) {
      onError(e.response?.data?.error ?? 'No se pudo eliminar el ítem.');
    } finally {
      setProcesando(null);
    }
  };

  const eliminarAdjunto = async (itemId: string, adjuntoId: string) => {
    if (!confirm('¿Eliminar este anexo?')) return;
    setProcesando(adjuntoId);
    onError(null);
    try {
      await cvApi.eliminarItemAdjunto(itemId, adjuntoId);
      onMensaje('Anexo eliminado.');
      onCambio();
    } catch (e: any) {
      onError(e.response?.data?.error ?? 'No se pudo eliminar el anexo.');
    } finally {
      setProcesando(null);
    }
  };

  const descargarAdjunto = async (itemId: string, adjuntoId: string, nombre: string) => {
    try {
      const { blob } = await cvApi.descargarItemAdjunto(itemId, adjuntoId);
      descargarBlob(blob, nombre || `anexo_${adjuntoId}.pdf`);
    } catch (e: any) {
      onError(e.response?.data?.error ?? 'No se pudo descargar el anexo.');
    }
  };

  const verAdjunto = async (itemId: string, adjuntoId: string, nombre: string) => {
    onError(null);
    try {
      const { blob } = await cvApi.descargarItemAdjunto(itemId, adjuntoId);
      onVer(blob, nombre);
    } catch (e: any) {
      onError(e.response?.data?.error ?? 'No se pudo abrir el anexo.');
    }
  };

  const grupos = [...new Set(mios.map((x) => x.categoria))].sort((a, b) => a.localeCompare(b));

  return (
    <div className="card space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="font-semibold">{titulo} ({mios.length})</h2>
        {!form && (
          <button onClick={() => { onError(null); setForm(nuevoForm()); }} className="btn-primary">
            <Plus size={16} /> {textoAgregar}
          </button>
        )}
      </div>

      {form && (
        <div className="space-y-3 rounded-lg border border-soft p-3">
          <div className="grid gap-3 sm:grid-cols-2">
            <div>
              <label className="label">Categoría</label>
              {categorias ? (
                <select value={form.categoria} onChange={(e) => setForm({ ...form, categoria: e.target.value })} className="input">
                  <option value="">Elegí una categoría...</option>
                  {categorias.map((c) => (
                    <option key={c} value={c}>{c}</option>
                  ))}
                </select>
              ) : (
                <input value={form.categoria} onChange={(e) => setForm({ ...form, categoria: e.target.value })} className="input" maxLength={300} placeholder="Ej: Otra participación destacada" />
              )}
            </div>
            <div>
              <label className="label">Título</label>
              <input value={form.titulo} onChange={(e) => setForm({ ...form, titulo: e.target.value })} className="input" maxLength={500} placeholder="Ej: Muestra Nacional de Artesanía 2025" />
            </div>
            <div>
              <label className="label">Institución / organización (opcional)</label>
              <input value={form.institucion} onChange={(e) => setForm({ ...form, institucion: e.target.value })} className="input" maxLength={300} placeholder="Ej: UNRN" />
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="label">Desde</label>
                <input type="date" value={form.fechaDesde} max={new Date().toISOString().slice(0, 10)} onChange={(e) => setForm({ ...form, fechaDesde: e.target.value })} className="input" />
              </div>
              <div>
                <label className="label">Hasta</label>
                <input type="date" value={form.actualidad ? '' : form.fechaHasta} disabled={form.actualidad} max={new Date().toISOString().slice(0, 10)} onChange={(e) => setForm({ ...form, fechaHasta: e.target.value })} className="input disabled:opacity-40" />
                <label className="mt-1 flex items-center gap-1 text-xs text-ink-secondary">
                  <input type="checkbox" checked={form.actualidad} onChange={(e) => setForm({ ...form, actualidad: e.target.checked })} />
                  Actualidad
                </label>
              </div>
            </div>
          </div>
          <div>
            <label className="label">Descripción (opcional)</label>
            <textarea
              value={form.descripcion}
              onChange={(e) => setForm({ ...form, descripcion: e.target.value })}
              maxLength={3000}
              rows={3}
              className="input resize-y"
              placeholder="Podés usar **negrita**, *cursiva* y listas con '-'"
            />
          </div>
          <div>
            <label className="label">Anexos (PDF, JPG o PNG · máx. 5 archivos · hasta 10 MB c/u)</label>
            <input
              type="file"
              multiple
              accept=".pdf,.jpg,.jpeg,.png"
              onChange={(e) => {
                const nuevos = Array.from(e.target.files ?? []);
                setForm({ ...form, archivos: [...form.archivos, ...nuevos].slice(0, 5) });
              }}
              className="input file:mr-3 file:rounded-pill file:border-0 file:bg-surface-alt file:px-3 file:py-1 file:text-sm"
            />
            {form.archivos.length > 0 && (
              <ul className="mt-2 space-y-1">
                {form.archivos.map((f, i) => (
                  <li key={i} className="flex items-center justify-between gap-2 text-sm text-ink-secondary">
                    <span className="min-w-0 truncate">{f.name}</span>
                    <button
                      type="button"
                      onClick={() => setForm({ ...form, archivos: form.archivos.filter((_, j) => j !== i) })}
                      className="btn-danger !px-2 !py-0.5 text-xs"
                    >
                      Quitar
                    </button>
                  </li>
                ))}
              </ul>
            )}
            {form.archivos.length >= 5 && <p className="text-xs text-ink-muted">Máximo 5 anexos por ítem.</p>}
          </div>
          <div className="flex gap-2">
            <button onClick={guardar} disabled={guardando} className="btn-primary">
              {guardando ? 'Guardando...' : form.id ? 'Guardar cambios' : 'Agregar'}
            </button>
            <button onClick={() => setForm(null)} className="btn-secondary">Cancelar</button>
          </div>
        </div>
      )}

      {mios.length > 0 ? (
        <div className="space-y-4">
          {grupos.map((cat) => (
            <div key={cat} className="space-y-2">
              <p className="text-xs font-semibold uppercase tracking-wide text-ink-muted">{cat}</p>
              {mios.filter((x) => x.categoria === cat).map((x) => (
                <div key={x.id} className="flex flex-wrap items-start justify-between gap-2 rounded-lg border border-soft p-3">
                  <div className="min-w-0">
                    <p className="font-medium">{x.titulo}{x.institucion ? ` — ${x.institucion}` : ''}</p>
                    <p className="text-sm text-ink-muted">
                      {formatFecha(x.fechaDesde)} – {x.fechaHasta ? formatFecha(x.fechaHasta) : 'Actualidad'}
                    </p>
                    {x.descripcion && <p className="mt-1 whitespace-pre-line text-sm text-ink-secondary">{x.descripcion}</p>}
                    {x.adjuntos && x.adjuntos.length > 0 && (
                      <div className="mt-2 space-y-1">
                        <p className="text-xs font-medium text-ink-muted">Anexos</p>
                        {x.adjuntos.map((adj) => (
                          <div key={adj.id} className="flex flex-wrap items-center gap-2 text-sm">
                            <span className="min-w-0 max-w-[14rem] truncate">{adj.nombreArchivo}</span>
                            <span className="text-xs text-ink-muted">({tamanoLegible(adj.tamanoBytes)})</span>
                            <button onClick={() => verAdjunto(x.id, adj.adjuntoId, adj.nombreArchivo)} className="btn-secondary !px-2 !py-0.5 text-xs" title="Vista previa">
                              <Eye size={12} /> Ver
                            </button>
                            <button onClick={() => descargarAdjunto(x.id, adj.adjuntoId, adj.nombreArchivo)} className="btn-secondary !px-2 !py-0.5 text-xs" title="Descargar">
                              <Download size={12} />
                            </button>
                            <button onClick={() => eliminarAdjunto(x.id, adj.adjuntoId)} className="btn-danger !px-2 !py-0.5 text-xs" title="Eliminar anexo">
                              <Trash2 size={12} />
                            </button>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                  <div className="flex items-center gap-1">
                    <button onClick={() => duplicar(x.id)} disabled={procesando === x.id} className="btn-secondary !px-2 !py-1" title="Duplicar">
                      <Copy size={14} />
                    </button>
                    <button onClick={() => abrirEditar(x)} className="btn-secondary !px-2 !py-1" title="Editar">
                      <Pencil size={14} />
                    </button>
                    <button onClick={() => eliminar(x.id)} disabled={procesando === x.id} className="btn-danger !px-2 !py-1" title="Eliminar">
                      <Trash2 size={14} />
                    </button>
                  </div>
                </div>
              ))}
            </div>
          ))}
        </div>
      ) : (
        !form && <p className="text-sm text-ink-secondary">Todavía no cargaste nada en esta sección.</p>
      )}
    </div>
  );
}
