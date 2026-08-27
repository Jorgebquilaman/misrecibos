import { useEffect, useState } from 'react';
import { Download, Eye, FileDown, FileUp, Pencil, Plus, Trash2 } from 'lucide-react';
import { cvApi } from '../api';
import type { AntecedenteAcademicoDto, CertificadoCvDto, ExperienciaCvDto } from '../types';
import { useAuthStore } from '../store/authStore';
import { descargarBlob, formatFecha, formatFechaHora } from '../utils';
import VisorArchivo, { type ArchivoParaVer } from '../components/VisorArchivo';

const TIPOS = [
  { valor: 'Curso', nombre: 'Curso' },
  { valor: 'Taller', nombre: 'Taller' },
  { valor: 'Diplomatura', nombre: 'Diplomatura' },
  { valor: 'Carrera', nombre: 'Carrera' },
  { valor: 'Posgrado', nombre: 'Posgrado' },
  { valor: 'Otro', nombre: 'Otro' }
];

const NIVELES = [
  { valor: 'Secundario', nombre: 'Secundario' },
  { valor: 'Terciario', nombre: 'Terciario' },
  { valor: 'Universitario', nombre: 'Universitario' },
  { valor: 'Posgrado', nombre: 'Posgrado' },
  { valor: 'Maestria', nombre: 'Maestría' },
  { valor: 'Doctorado', nombre: 'Doctorado' },
  { valor: 'Otro', nombre: 'Otro' }
];

const ETIQUETA_ESTADO_CV: Record<string, string> = {
  Pendiente: 'En revisión',
  Verificado: 'Verificado',
  Observado: 'Observado'
};

type Solapa = 'resumen' | 'experiencia' | 'antecedentes' | 'certificados';

const SOLAPAS: { id: Solapa; nombre: string }[] = [
  { id: 'resumen', nombre: 'Resumen y datos' },
  { id: 'experiencia', nombre: 'Experiencia profesional' },
  { id: 'antecedentes', nombre: 'Antecedentes académicos' },
  { id: 'certificados', nombre: 'Certificados' }
];

export default function MiCvPage() {
  const usuario = useAuthStore((s) => s.usuario);
  const [mios, setMios] = useState<CertificadoCvDto[]>([]);
  const [nombre, setNombre] = useState('');
  const [institucion, setInstitucion] = useState('');
  const [tipo, setTipo] = useState('Curso');
  const [fechaObtencion, setFechaObtencion] = useState('');
  const [archivo, setArchivo] = useState<File | null>(null);
  const [mensaje, setMensaje] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);
  const [viendo, setViendo] = useState<ArchivoParaVer | null>(null);
  const [descargandoCv, setDescargandoCv] = useState(false);
  const [observaciones, setObservaciones] = useState<string>('');
  const [guardandoObs, setGuardandoObs] = useState(false);
  const [telefono, setTelefono] = useState<string>('');
  const [guardandoTel, setGuardandoTel] = useState(false);
  const [experiencias, setExperiencias] = useState<ExperienciaCvDto[]>([]);
  const [expForm, setExpForm] = useState<{ id?: string; puesto: string; institucion: string; descripcion: string; fechaDesde: string; fechaHasta: string; actualidad: boolean } | null>(null);
  const [antecedentes, setAntecedentes] = useState<AntecedenteAcademicoDto[]>([]);
  const [antForm, setAntForm] = useState<{ id?: string; titulo: string; institucion: string; nivel: string; descripcion: string; fechaDesde: string; fechaHasta: string; enCurso: boolean; archivo: File | null } | null>(null);
  const [solapa, setSolapa] = useState<Solapa>('resumen');

  const cargar = () => {
    cvApi.mios().then(setMios).catch(() => {});
    cvApi.observaciones().then((o) => setObservaciones(o ?? '')).catch(() => {});
    cvApi.telefono().then((t) => setTelefono(t ?? '')).catch(() => {});
    cvApi.experiencias().then(setExperiencias).catch(() => {});
    cvApi.antecedentes().then(setAntecedentes).catch(() => {});
  };

  useEffect(() => { cargar(); }, []);

  const subir = async () => {
    if (!nombre.trim() || !institucion.trim() || !fechaObtencion || !archivo) {
      setError('Completá nombre, institución, fecha y archivo del certificado.');
      return;
    }
    if (archivo.size > 10 * 1024 * 1024) {
      setError('El archivo supera los 10 MB.');
      return;
    }
    setEnviando(true);
    setError(null);
    try {
      await cvApi.subir({ nombre, institucion, tipo, fechaObtencion, archivo });
      setMensaje('Certificado subido. RR.HH. lo va a revisar.');
      setNombre('');
      setInstitucion('');
      setFechaObtencion('');
      setArchivo(null);
      (document.getElementById('cv-archivo') as HTMLInputElement | null)?.value && ((document.getElementById('cv-archivo') as HTMLInputElement).value = '');
      cargar();
    } catch (e: any) {
      setError(e.response?.data?.error ?? 'No se pudo subir el certificado.');
    } finally {
      setEnviando(false);
    }
  };

  const descargar = async (id: string) => {
    try {
      const { blob } = await cvApi.descargarArchivo(id);
      const c = mios.find((x) => x.id === id);
      descargarBlob(blob, c?.nombreArchivo ?? `certificado_${id}.pdf`);
    } catch (e: any) {
      setError(e.response?.data?.error ?? 'No se pudo descargar el archivo.');
    }
  };

  const ver = async (id: string) => {
    setError(null);
    try {
      const { blob } = await cvApi.descargarArchivo(id);
      const c = mios.find((x) => x.id === id);
      setViendo({ blob, nombre: c?.nombreArchivo ?? 'certificado.pdf' });
    } catch (e: any) {
      setError(e.response?.data?.error ?? 'No se pudo abrir el archivo.');
    }
  };

  const guardarObservaciones = async () => {
    setGuardandoObs(true);
    setError(null);
    try {
      await cvApi.guardarObservaciones(observaciones.trim() || null);
      setMensaje('Resumen profesional guardado. Se incluye en tu CV.');
    } catch (e: any) {
      setError(e.response?.data?.error ?? 'No se pudo guardar el resumen profesional.');
    } finally {
      setGuardandoObs(false);
    }
  };

  const guardarTelefono = async () => {
    setGuardandoTel(true);
    setError(null);
    try {
      await cvApi.guardarTelefono(telefono.trim() || null);
      setMensaje('Teléfono guardado. Se incluye en tu CV.');
    } catch (e: any) {
      setError(e.response?.data?.error ?? 'No se pudo guardar el teléfono.');
    } finally {
      setGuardandoTel(false);
    }
  };

  const guardarExperiencia = async () => {
    if (!expForm) return;
    if (!expForm.puesto.trim() || !expForm.institucion.trim() || !expForm.fechaDesde) {
      setError('Completá puesto, institución y fecha de inicio.');
      return;
    }
    setError(null);
    const data = {
      puesto: expForm.puesto,
      institucion: expForm.institucion,
      descripcion: expForm.descripcion || null,
      fechaDesde: expForm.fechaDesde,
      fechaHasta: expForm.actualidad ? null : (expForm.fechaHasta || null)
    };
    try {
      if (expForm.id) await cvApi.editarExperiencia(expForm.id, data);
      else await cvApi.crearExperiencia(data);
      setMensaje('Experiencia guardada. Se incluye en tu CV.');
      setExpForm(null);
      cargar();
    } catch (e: any) {
      setError(e.response?.data?.error ?? 'No se pudo guardar la experiencia.');
    }
  };

  const eliminarExperiencia = async (id: string) => {
    if (!confirm('¿Eliminar esta experiencia laboral?')) return;
    setError(null);
    try {
      await cvApi.eliminarExperiencia(id);
      setMensaje('Experiencia eliminada.');
      cargar();
    } catch (e: any) {
      setError(e.response?.data?.error ?? 'No se pudo eliminar la experiencia.');
    }
  };

  const guardarAntecedente = async () => {
    if (!antForm) return;
    if (!antForm.titulo.trim() || !antForm.institucion.trim() || !antForm.fechaDesde) {
      setError('Completá título, institución y fecha de inicio.');
      return;
    }
    if (!antForm.id && !antForm.archivo) {
      setError('Adjuntá el título escaneado (PDF, JPG o PNG).');
      return;
    }
    if (antForm.archivo && antForm.archivo.size > 10 * 1024 * 1024) {
      setError('El archivo supera los 10 MB.');
      return;
    }
    setError(null);
    try {
      if (antForm.id) {
        await cvApi.editarAntecedente(antForm.id, {
          titulo: antForm.titulo,
          institucion: antForm.institucion,
          nivel: antForm.nivel,
          descripcion: antForm.descripcion || null,
          fechaDesde: antForm.fechaDesde,
          fechaHasta: antForm.enCurso ? null : (antForm.fechaHasta || null)
        });
        setMensaje('Antecedente académico actualizado. Se incluye en tu CV.');
      } else {
        await cvApi.crearAntecedente({
          titulo: antForm.titulo,
          institucion: antForm.institucion,
          nivel: antForm.nivel,
          descripcion: antForm.descripcion || null,
          fechaDesde: antForm.fechaDesde,
          fechaHasta: antForm.enCurso ? null : (antForm.fechaHasta || null),
          archivo: antForm.archivo!
        });
        setMensaje('Antecedente académico agregado. Se incluye en tu CV.');
      }
      setAntForm(null);
      cargar();
    } catch (e: any) {
      setError(e.response?.data?.error ?? 'No se pudo guardar el antecedente académico.');
    }
  };

  const eliminarAntecedente = async (id: string) => {
    if (!confirm('¿Eliminar este antecedente académico?')) return;
    setError(null);
    try {
      await cvApi.eliminarAntecedente(id);
      setMensaje('Antecedente eliminado.');
      cargar();
    } catch (e: any) {
      setError(e.response?.data?.error ?? 'No se pudo eliminar el antecedente.');
    }
  };

  const verAntecedente = async (id: string) => {
    setError(null);
    try {
      const { blob } = await cvApi.descargarAntecedente(id);
      const a = antecedentes.find((x) => x.id === id);
      setViendo({ blob, nombre: a?.nombreArchivo ?? 'antecedente.pdf' });
    } catch (e: any) {
      setError(e.response?.data?.error ?? 'No se pudo abrir el archivo.');
    }
  };

  const descargarAntecedente = async (id: string) => {
    try {
      const { blob } = await cvApi.descargarAntecedente(id);
      const a = antecedentes.find((x) => x.id === id);
      descargarBlob(blob, a?.nombreArchivo ?? `antecedente_${id}.pdf`);
    } catch (e: any) {
      setError(e.response?.data?.error ?? 'No se pudo descargar el archivo.');
    }
  };

  const descargarCv = async () => {
    setDescargandoCv(true);
    setError(null);
    try {
      const blob = await cvApi.descargarCv();
      descargarBlob(blob, `CV_${usuario?.nombre ?? ''}_${usuario?.correo ?? ''}.pdf`.replace(/\s+/g, '_'));
    } catch (e: any) {
      setError(e.response?.data?.error ?? 'No se pudo generar el CV.');
    } finally {
      setDescargandoCv(false);
    }
  };

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <div>
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h1 className="text-2xl font-bold">Mi CV</h1>
            <p className="text-sm text-ink-secondary">Gestioná tu resumen profesional, experiencia, antecedentes académicos y certificados para armar tu CV.</p>
          </div>
          <button onClick={descargarCv} disabled={descargandoCv} className="btn-primary">
            <FileDown size={16} /> {descargandoCv ? 'Generando...' : 'Descargar mi CV (PDF)'}
          </button>
        </div>
      </div>

      {mensaje && <p className="rounded-lg tint-success px-3 py-2 text-sm">{mensaje}</p>}
      {error && <p className="rounded-lg tint-danger px-3 py-2 text-sm">{error}</p>}

      <div className="flex flex-wrap gap-2">
        {SOLAPAS.map((s) => (
          <button
            key={s.id}
            onClick={() => setSolapa(s.id)}
            className={`btn !px-4 !py-1.5 text-sm ${solapa === s.id ? 'bg-accent text-accent-ink' : 'btn-secondary'}`}
          >
            {s.nombre}
            {s.id === 'experiencia' && experiencias.length > 0 && (
              <span className="rounded-pill bg-black/10 px-1.5 text-xs dark:bg-white/10">{experiencias.length}</span>
            )}
            {s.id === 'antecedentes' && antecedentes.length > 0 && (
              <span className="rounded-pill bg-black/10 px-1.5 text-xs dark:bg-white/10">{antecedentes.length}</span>
            )}
            {s.id === 'certificados' && mios.length > 0 && (
              <span className="rounded-pill bg-black/10 px-1.5 text-xs dark:bg-white/10">{mios.length}</span>
            )}
          </button>
        ))}
      </div>

      {solapa === 'resumen' && (
      <div className="card space-y-3">
        <h2 className="font-semibold">Resumen profesional</h2>
        <p className="text-sm text-ink-secondary">
          Perfil, habilidades, idiomas, disponibilidad... Podés usar formato: <b>**negrita**</b>, <i>*cursiva*</i>,{' '}
          <u>__subrayado__</u>, ~~tachado~~ y listas con "-".
        </p>
        <textarea
          value={observaciones}
          onChange={(e) => setObservaciones(e.target.value)}
          maxLength={2000}
          rows={5}
          className="input resize-y"
          placeholder="Ej: Técnico universitario en curso. Manejo de PostgreSQL y herramientas ofimáticas. Disponibilidad horaria full-time."
        />
        <p className="text-xs text-ink-muted">{observaciones.length}/2000 caracteres</p>

        <div className="flex flex-wrap items-end gap-3 border-t border-soft pt-3">
          <div>
            <label className="label">Teléfono de contacto (opcional)</label>
            <input
              value={telefono}
              onChange={(e) => setTelefono(e.target.value)}
              maxLength={50}
              className="input !w-56"
              placeholder="Ej: +54 298 4XX-XXXX"
            />
          </div>
          <button onClick={guardarTelefono} disabled={guardandoTel} className="btn-secondary">
            {guardandoTel ? 'Guardando...' : 'Guardar teléfono'}
          </button>
        </div>

        <button onClick={guardarObservaciones} disabled={guardandoObs} className="btn-primary">
          {guardandoObs ? 'Guardando...' : 'Guardar resumen profesional'}
        </button>
      </div>
      )}

      {solapa === 'experiencia' && (
      <div className="card space-y-3">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h2 className="font-semibold">Experiencia profesional ({experiencias.length})</h2>
          {!expForm && (
            <button onClick={() => setExpForm({ puesto: '', institucion: '', descripcion: '', fechaDesde: '', fechaHasta: '', actualidad: false })} className="btn-primary">
              <Plus size={16} /> Agregar experiencia
            </button>
          )}
        </div>

        {expForm && (
          <div className="space-y-3 rounded-lg border border-soft p-3">
            <div className="grid gap-3 sm:grid-cols-2">
              <div>
                <label className="label">Puesto / cargo</label>
                <input value={expForm.puesto} onChange={(e) => setExpForm({ ...expForm, puesto: e.target.value })} className="input" placeholder="Ej: Administrador de sistemas" />
              </div>
              <div>
                <label className="label">Institución / empresa</label>
                <input value={expForm.institucion} onChange={(e) => setExpForm({ ...expForm, institucion: e.target.value })} className="input" placeholder="Ej: IUPA" />
              </div>
              <div>
                <label className="label">Desde</label>
                <input type="date" value={expForm.fechaDesde} max={new Date().toISOString().slice(0, 10)} onChange={(e) => setExpForm({ ...expForm, fechaDesde: e.target.value })} className="input" />
              </div>
              <div>
                <label className="label">Hasta</label>
                <input
                  type="date"
                  value={expForm.actualidad ? '' : expForm.fechaHasta}
                  disabled={expForm.actualidad}
                  max={new Date().toISOString().slice(0, 10)}
                  onChange={(e) => setExpForm({ ...expForm, fechaHasta: e.target.value })}
                  className="input disabled:opacity-40"
                />
                <label className="mt-1 flex items-center gap-1 text-xs text-ink-secondary">
                  <input
                    type="checkbox"
                    checked={expForm.actualidad}
                    onChange={(e) => setExpForm({ ...expForm, actualidad: e.target.checked })}
                  />
                  Actualidad
                </label>
              </div>
            </div>
            <div>
              <label className="label">Descripción de tareas y logros (opcional)</label>
              <textarea
                value={expForm.descripcion}
                onChange={(e) => setExpForm({ ...expForm, descripcion: e.target.value })}
                maxLength={3000}
                rows={3}
                className="input resize-y"
                placeholder="Podés usar **negrita**, *cursiva* y listas con '-'"
              />
            </div>
            <div className="flex gap-2">
              <button onClick={guardarExperiencia} className="btn-primary">
                {expForm.id ? 'Guardar cambios' : 'Agregar experiencia'}
              </button>
              <button onClick={() => setExpForm(null)} className="btn-secondary">Cancelar</button>
            </div>
          </div>
        )}

        {experiencias.length > 0 ? (
          <div className="space-y-2">
            {experiencias.map((x) => (
              <div key={x.id} className="flex flex-wrap items-start justify-between gap-2 rounded-lg border border-soft p-3">
                <div className="min-w-0">
                  <p className="font-medium">
                    {x.puesto} — {x.institucion}
                  </p>
                  <p className="text-sm text-ink-muted">
                    {formatFecha(x.fechaDesde)} – {x.fechaHasta ? formatFecha(x.fechaHasta) : 'Actualidad'}
                  </p>
                  {x.descripcion && <p className="mt-1 whitespace-pre-line text-sm text-ink-secondary">{x.descripcion}</p>}
                </div>
                <div className="flex items-center gap-1">
                  <button
                    onClick={() => {
                      setError(null);
                      setExpForm({
                        id: x.id,
                        puesto: x.puesto,
                        institucion: x.institucion,
                        descripcion: x.descripcion ?? '',
                        fechaDesde: x.fechaDesde.slice(0, 10),
                        fechaHasta: x.fechaHasta?.slice(0, 10) ?? '',
                        actualidad: !x.fechaHasta
                      });
                      window.scrollTo({ top: 0, behavior: 'smooth' });
                    }}
                    className="btn-secondary !px-2 !py-1"
                    title="Editar"
                  >
                    <Pencil size={14} />
                  </button>
                  <button onClick={() => eliminarExperiencia(x.id!)} className="btn-danger !px-2 !py-1" title="Eliminar">
                    <Trash2 size={14} />
                  </button>
                </div>
              </div>
            ))}
          </div>
        ) : (
          !expForm && <p className="text-sm text-ink-secondary">Todavía no cargaste experiencias laborales.</p>
        )}
      </div>
      )}

      {solapa === 'antecedentes' && (
      <div className="card space-y-3">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h2 className="font-semibold">Antecedentes académicos ({antecedentes.length})</h2>
          {!antForm && (
            <button onClick={() => setAntForm({ titulo: '', institucion: '', nivel: 'Universitario', descripcion: '', fechaDesde: '', fechaHasta: '', enCurso: false, archivo: null })} className="btn-primary">
              <Plus size={16} /> Agregar antecedente
            </button>
          )}
        </div>

        {antForm && (
          <div className="space-y-3 rounded-lg border border-soft p-3">
            <div className="grid gap-3 sm:grid-cols-2">
              <div>
                <label className="label">Título / carrera</label>
                <input value={antForm.titulo} onChange={(e) => setAntForm({ ...antForm, titulo: e.target.value })} className="input" placeholder="Ej: Profesorado de Música" />
              </div>
              <div>
                <label className="label">Institución</label>
                <input value={antForm.institucion} onChange={(e) => setAntForm({ ...antForm, institucion: e.target.value })} className="input" placeholder="Ej: IUPA" />
              </div>
              <div>
                <label className="label">Nivel</label>
                <select value={antForm.nivel} onChange={(e) => setAntForm({ ...antForm, nivel: e.target.value })} className="input">
                  {NIVELES.map((n) => (
                    <option key={n.valor} value={n.valor}>{n.nombre}</option>
                  ))}
                </select>
              </div>
              <div>
                <label className="label">Desde</label>
                <input type="date" value={antForm.fechaDesde} max={new Date().toISOString().slice(0, 10)} onChange={(e) => setAntForm({ ...antForm, fechaDesde: e.target.value })} className="input" />
              </div>
              <div>
                <label className="label">Hasta</label>
                <input
                  type="date"
                  value={antForm.enCurso ? '' : antForm.fechaHasta}
                  disabled={antForm.enCurso}
                  max={new Date().toISOString().slice(0, 10)}
                  onChange={(e) => setAntForm({ ...antForm, fechaHasta: e.target.value })}
                  className="input disabled:opacity-40"
                />
                <label className="mt-1 flex items-center gap-1 text-xs text-ink-secondary">
                  <input type="checkbox" checked={antForm.enCurso} onChange={(e) => setAntForm({ ...antForm, enCurso: e.target.checked })} />
                  En curso
                </label>
              </div>
              {!antForm.id && (
                <div className="sm:col-span-2">
                  <label className="label">Título escaneado (PDF, JPG o PNG · máx. 10 MB)</label>
                  <input
                    type="file"
                    accept=".pdf,.jpg,.jpeg,.png"
                    onChange={(e) => setAntForm({ ...antForm, archivo: e.target.files?.[0] ?? null })}
                    className="input file:mr-3 file:rounded-pill file:border-0 file:bg-surface-alt file:px-3 file:py-1 file:text-sm"
                  />
                </div>
              )}
            </div>
            <div>
              <label className="label">Descripción (opcional)</label>
              <textarea
                value={antForm.descripcion}
                onChange={(e) => setAntForm({ ...antForm, descripcion: e.target.value })}
                maxLength={3000}
                rows={3}
                className="input resize-y"
                placeholder="Ej: Promedio 8.5, tesis sobre..."
              />
            </div>
            <div className="flex gap-2">
              <button onClick={guardarAntecedente} className="btn-primary">
                {antForm.id ? 'Guardar cambios' : 'Agregar antecedente'}
              </button>
              <button onClick={() => setAntForm(null)} className="btn-secondary">Cancelar</button>
            </div>
            {antForm.id && <p className="text-xs text-ink-muted">El archivo no se puede cambiar al editar; eliminá y volvé a cargar si necesitás reemplazarlo.</p>}
          </div>
        )}

        {antecedentes.length > 0 ? (
          <div className="space-y-2">
            {antecedentes.map((a) => (
              <div key={a.id} className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-soft p-3">
                <div className="min-w-0">
                  <p className="font-medium">{a.titulo} — {a.institucion} · {NIVELES.find((n) => n.valor === a.nivel)?.nombre ?? a.nivel}</p>
                  <p className="text-sm text-ink-muted">
                    {formatFecha(a.fechaDesde)} – {a.fechaHasta ? formatFecha(a.fechaHasta) : 'En curso'} · {a.nombreArchivo}
                  </p>
                  {a.descripcion && <p className="mt-1 whitespace-pre-line text-sm text-ink-secondary">{a.descripcion}</p>}
                </div>
                <div className="flex items-center gap-1">
                  <button onClick={() => verAntecedente(a.id)} className="btn-secondary !px-2 !py-1" title="Vista previa">
                    <Eye size={14} /> Ver
                  </button>
                  <button onClick={() => descargarAntecedente(a.id)} className="btn-secondary !px-2 !py-1" title="Descargar">
                    <Download size={14} />
                  </button>
                  <button
                    onClick={() => {
                      setError(null);
                      setAntForm({
                        id: a.id,
                        titulo: a.titulo,
                        institucion: a.institucion,
                        nivel: a.nivel,
                        descripcion: a.descripcion ?? '',
                        fechaDesde: a.fechaDesde.slice(0, 10),
                        fechaHasta: a.fechaHasta?.slice(0, 10) ?? '',
                        enCurso: !a.fechaHasta,
                        archivo: null
                      });
                      window.scrollTo({ top: 0, behavior: 'smooth' });
                    }}
                    className="btn-secondary !px-2 !py-1"
                    title="Editar"
                  >
                    <Pencil size={14} />
                  </button>
                  <button onClick={() => eliminarAntecedente(a.id)} className="btn-danger !px-2 !py-1" title="Eliminar">
                    <Trash2 size={14} />
                  </button>
                </div>
              </div>
            ))}
          </div>
        ) : (
          !antForm && <p className="text-sm text-ink-secondary">Todavía no cargaste antecedentes académicos.</p>
        )}
      </div>
      )}

      {solapa === 'certificados' && (
      <>
      <div className="card">
        <h2 className="mb-3 font-semibold">Subir certificado</h2>
        <div className="grid gap-3 sm:grid-cols-2">
          <div>
            <label className="label">Nombre del curso / carrera</label>
            <input value={nombre} onChange={(e) => setNombre(e.target.value)} className="input" placeholder="Ej: Analista de Sistemas" />
          </div>
          <div>
            <label className="label">Institución</label>
            <input value={institucion} onChange={(e) => setInstitucion(e.target.value)} className="input" placeholder="Ej: UNPA" />
          </div>
          <div>
            <label className="label">Tipo</label>
            <select value={tipo} onChange={(e) => setTipo(e.target.value)} className="input">
              {TIPOS.map((t) => (
                <option key={t.valor} value={t.valor}>{t.nombre}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="label">Fecha de obtención</label>
            <input type="date" value={fechaObtencion} onChange={(e) => setFechaObtencion(e.target.value)} max={new Date().toISOString().slice(0, 10)} className="input" />
          </div>
          <div className="sm:col-span-2">
            <label className="label">Certificado (PDF, JPG o PNG · máx. 10 MB)</label>
            <input
              id="cv-archivo"
              type="file"
              accept=".pdf,.jpg,.jpeg,.png"
              onChange={(e) => setArchivo(e.target.files?.[0] ?? null)}
              className="input file:mr-3 file:rounded-pill file:border-0 file:bg-surface-alt file:px-3 file:py-1 file:text-sm"
            />
          </div>
        </div>
        <button onClick={subir} disabled={enviando} className="btn-primary mt-4">
          <FileUp size={16} /> {enviando ? 'Subiendo...' : 'Subir certificado'}
        </button>
      </div>

      <div className="card">
        <h2 className="mb-3 font-semibold">Mis certificados ({mios.length})</h2>
        {mios.length > 0 ? (
          <div className="space-y-2">
            {mios.map((c) => (
              <div key={c.id} className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-soft p-3">
                <div className="min-w-0">
                  <p className="font-medium">{c.nombre}</p>
                  <p className="text-sm text-ink-secondary">
                    {c.institucion} · {TIPOS.find((t) => t.valor === c.tipo)?.nombre ?? c.tipo} ·{' '}
                    {formatFecha(c.fechaObtencion)} · subido {formatFechaHora(c.fechaCarga)}
                  </p>
                  {c.comentarioRevision && (
                    <p className="mt-1 text-xs tint-warning rounded-pill inline-block px-2 py-0.5">Obs.: {c.comentarioRevision}</p>
                  )}
                </div>
                <div className="flex items-center gap-2">
                  <span className={`badge ${c.estado === 'Verificado' ? 'tint-success' : c.estado === 'Observado' ? 'tint-danger' : 'tint-warning'}`}>
                    {ETIQUETA_ESTADO_CV[c.estado] ?? c.estado}
                  </span>
                  <button onClick={() => ver(c.id)} className="btn-secondary" title="Vista previa">
                    <Eye size={16} /> Ver
                  </button>
                  <button onClick={() => descargar(c.id)} className="btn-secondary">
                    <Download size={16} /> Archivo
                  </button>
                </div>
              </div>
            ))}
          </div>
        ) : (
          <p className="text-sm text-ink-secondary">Todavía no subiste certificados.</p>
        )}
      </div>
      </>
      )}

      {viendo && <VisorArchivo archivo={viendo} onCerrar={() => setViendo(null)} />}
    </div>
  );
}
