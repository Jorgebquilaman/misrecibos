import { useEffect, useMemo, useState } from 'react';
import { Check, ChevronLeft, ChevronRight, Download, Eye, EyeOff } from 'lucide-react';
import { cvApi } from '../../api';
import type { CertificadoCvAdminDto } from '../../types';
import { descargarBlob, formatFecha, formatFechaHora } from '../../utils';
import VisorArchivo, { type ArchivoParaVer } from '../../components/VisorArchivo';

const TIPOS: Record<string, string> = {
  Curso: 'Curso',
  Taller: 'Taller',
  Diplomatura: 'Diplomatura',
  Carrera: 'Carrera',
  Posgrado: 'Posgrado',
  Otro: 'Otro'
};

const ESTADOS = [
  { valor: '', nombre: 'Todos' },
  { valor: 'Pendiente', nombre: 'En revisión' },
  { valor: 'Verificado', nombre: 'Verificados' },
  { valor: 'Observado', nombre: 'Observados' }
];

const TAMANOS = [15, 25, 50, 100];

export default function AdminCertificadosCvPage() {
  const [lista, setLista] = useState<CertificadoCvAdminDto[]>([]);
  const [filtro, setFiltro] = useState('');
  const [texto, setTexto] = useState('');
  const [pagina, setPagina] = useState(1);
  const [tamano, setTamano] = useState(25);
  const [error, setError] = useState<string | null>(null);
  const [mensaje, setMensaje] = useState<string | null>(null);
  const [comentario, setComentario] = useState<{ id: string; texto: string } | null>(null);
  const [ocupado, setOcupado] = useState<string | null>(null);
  const [viendo, setViendo] = useState<ArchivoParaVer | null>(null);
  const [fEmpleado, setFEmpleado] = useState('');
  const [fCurso, setFCurso] = useState('');
  const [fInstitucion, setFInstitucion] = useState('');
  const [fTipo, setFTipo] = useState('');
  const [fObtDesde, setFObtDesde] = useState('');
  const [fObtHasta, setFObtHasta] = useState('');

  const cargar = () => cvApi.admin(filtro || undefined).then(setLista).catch(() => setError('No se pudo cargar el listado.'));

  useEffect(() => { setPagina(1); cargar(); }, [filtro]);

  const limpiarFiltros = () => {
    setFEmpleado(''); setFCurso(''); setFInstitucion(''); setFTipo(''); setFObtDesde(''); setFObtHasta('');
    setPagina(1);
  };

  const filtrados = useMemo(() => {
    const q = texto.trim().toLowerCase();
    const emp = fEmpleado.trim().toLowerCase();
    const cur = fCurso.trim().toLowerCase();
    const inst = fInstitucion.trim().toLowerCase();
    return lista.filter((c) => {
      if (q && !`${c.legajo} ${c.empleadoNombre} ${c.nombre} ${c.institucion}`.toLowerCase().includes(q)) return false;
      if (emp && !`${c.legajo} ${c.empleadoNombre}`.toLowerCase().includes(emp)) return false;
      if (cur && !c.nombre.toLowerCase().includes(cur)) return false;
      if (inst && !c.institucion.toLowerCase().includes(inst)) return false;
      if (fTipo && c.tipo !== fTipo) return false;
      if (fObtDesde && c.fechaObtencion < fObtDesde) return false;
      if (fObtHasta && c.fechaObtencion > fObtHasta) return false;
      return true;
    });
  }, [lista, texto, fEmpleado, fCurso, fInstitucion, fTipo, fObtDesde, fObtHasta]);

  const total = filtrados.length;
  const totalPaginas = Math.max(1, Math.ceil(total / tamano));
  const paginaActual = Math.min(pagina, totalPaginas);
  const paginados = filtrados.slice((paginaActual - 1) * tamano, paginaActual * tamano);
  const desde = total === 0 ? 0 : (paginaActual - 1) * tamano + 1;
  const hasta = Math.min(paginaActual * tamano, total);

  const revisar = async (id: string, verificado: boolean) => {
    setOcupado(id);
    setError(null);
    try {
      await cvApi.revisar(id, verificado, comentario?.id === id ? comentario.texto || undefined : undefined);
      setComentario(null);
      cargar();
    } catch (e: any) {
      setError(e.response?.data?.error ?? 'No se pudo registrar la revisión.');
    } finally {
      setOcupado(null);
    }
  };

  const aprobarTodos = async () => {
    const pendientes = filtrados.filter((c) => c.estado === 'Pendiente').length;
    if (pendientes === 0) return;
    if (!confirm(`¿Aprobar ${pendientes} certificado(s) pendiente(s) filtrado(s)? Se marcarán como Verificados.`)) return;
    setOcupado('todos');
    setError(null);
    setMensaje(null);
    try {
      const r = await cvApi.aprobarTodos();
      const n = (r as any)?.aprobados ?? pendientes;
      setMensaje(`Se aprobaron ${n} certificado(s).`);
      cargar();
    } catch (e: any) {
      setError(e.response?.data?.error ?? 'No se pudo aprobar todos.');
    } finally {
      setOcupado(null);
    }
  };

  const descargar = async (id: string) => {
    try {
      const { blob, disposition } = await cvApi.descargarArchivo(id);
      const match = /filename="?([^";]+)"?/.exec(disposition ?? '');
      descargarBlob(blob, match?.[1] ?? `certificado_${id}.pdf`);
    } catch (e: any) {
      setError(e.response?.data?.error ?? 'No se pudo descargar el archivo.');
    }
  };

  const ver = async (c: CertificadoCvAdminDto) => {
    setError(null);
    try {
      const { blob } = await cvApi.descargarArchivo(c.id);
      setViendo({ blob, nombre: c.nombreArchivo || `certificado_${c.id}.pdf` });
    } catch (e: any) {
      setError(e.response?.data?.error ?? 'No se pudo abrir el archivo.');
    }
  };

  const pendientesCount = filtrados.filter((c) => c.estado === 'Pendiente').length;

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold">Certificados CV</h1>
          <p className="text-sm text-ink-secondary">Certificados de cursos y carreras que suben los empleados.</p>
        </div>
        <button
          onClick={aprobarTodos}
          disabled={ocupado === 'todos' || pendientesCount === 0}
          className="btn-primary !px-4 disabled:opacity-50"
          title={pendientesCount === 0 ? 'No hay pendientes filtrados' : `Aprobar ${pendientesCount} pendiente(s)`}
        >
          <Check size={16} /> {ocupado === 'todos' ? 'Aprobando...' : `Aprobar todos${pendientesCount ? ` (${pendientesCount})` : ''}`}
        </button>
      </div>

      {error && <p className="rounded-lg tint-danger px-3 py-2 text-sm">{error}</p>}
      {mensaje && <p className="rounded-lg tint-success px-3 py-2 text-sm">{mensaje}</p>}

      <div className="flex flex-wrap items-center gap-2">
        <select value={filtro} onChange={(e) => setFiltro(e.target.value)} className="input !w-auto">
          {ESTADOS.map((e) => (
            <option key={e.valor} value={e.valor}>{e.nombre}</option>
          ))}
        </select>
        <input
          value={texto}
          onChange={(e) => { setTexto(e.target.value); setPagina(1); }}
          placeholder="Buscar por legajo, apellido o curso..."
          className="input !w-auto flex-1"
        />
      </div>

      <div className="card overflow-x-auto">
        {lista.length > 0 ? (
          <table className="w-full text-left text-sm">
            <thead>
              <tr className="border-b text-ink-secondary">
                <th className="py-2 pr-3">Empleado</th>
                <th className="py-2 pr-3">Curso / carrera</th>
                <th className="py-2 pr-3">Institución</th>
                <th className="py-2 pr-3">Tipo</th>
                <th className="py-2 pr-3">Obtención</th>
                <th className="py-2 pr-3">Subido</th>
                <th className="py-2 pr-3">Estado</th>
                <th className="py-2"></th>
              </tr>
              <tr className="text-ink-secondary">
                <th className="py-1 pr-2 font-normal">
                  <input value={fEmpleado} onChange={(e) => { setFEmpleado(e.target.value); setPagina(1); }}
                    placeholder="Legajo o apellido…" className="input !w-full !px-2 !py-1 !text-xs" />
                </th>
                <th className="py-1 pr-2 font-normal">
                  <input value={fCurso} onChange={(e) => { setFCurso(e.target.value); setPagina(1); }}
                    placeholder="Curso…" className="input !w-full !px-2 !py-1 !text-xs" />
                </th>
                <th className="py-1 pr-2 font-normal">
                  <input value={fInstitucion} onChange={(e) => { setFInstitucion(e.target.value); setPagina(1); }}
                    placeholder="Institución…" className="input !w-full !px-2 !py-1 !text-xs" />
                </th>
                <th className="py-1 pr-2 font-normal">
                  <select value={fTipo} onChange={(e) => { setFTipo(e.target.value); setPagina(1); }}
                    className="input !w-full !px-2 !py-1 !text-xs">
                    <option value="">Todos</option>
                    {Object.entries(TIPOS).map(([v, n]) => (
                      <option key={v} value={v}>{n}</option>
                    ))}
                  </select>
                </th>
                <th className="py-1 pr-2 font-normal">
                  <div className="flex flex-col gap-1">
                    <input type="date" value={fObtDesde} onChange={(e) => { setFObtDesde(e.target.value); setPagina(1); }}
                      title="Desde" className="input !w-full !px-1 !py-0.5 !text-[11px]" />
                    <input type="date" value={fObtHasta} onChange={(e) => { setFObtHasta(e.target.value); setPagina(1); }}
                      title="Hasta" className="input !w-full !px-1 !py-0.5 !text-[11px]" />
                  </div>
                </th>
                <th className="py-1 pr-2 font-normal"></th>
                <th className="py-1 font-normal">
                  {(fEmpleado || fCurso || fInstitucion || fTipo || fObtDesde || fObtHasta) && (
                    <button onClick={limpiarFiltros} className="btn-secondary !px-2 !py-1 !text-xs" title="Limpiar filtros">
                      Limpiar
                    </button>
                  )}
                </th>
                <th className="py-2"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-soft">
              {paginados.map((c) => (
                <tr key={c.id}>
                  <td className="py-2 pr-3">
                    <p className="font-medium">{c.legajo}</p>
                    <p className="text-xs text-ink-secondary">{c.empleadoNombre}</p>
                  </td>
                  <td className="py-2 pr-3">
                    {c.nombre}
                    {c.comentarioRevision && (
                      <p className="text-xs tint-warning rounded-pill inline-block px-2 py-0.5 mt-1">Obs.: {c.comentarioRevision}</p>
                    )}
                  </td>
                  <td className="py-2 pr-3">{c.institucion}</td>
                  <td className="py-2 pr-3">{TIPOS[c.tipo] ?? c.tipo}</td>
                  <td className="py-2 pr-3">{formatFecha(c.fechaObtencion)}</td>
                  <td className="py-2 pr-3">{formatFechaHora(c.fechaCarga)}</td>
                  <td className="py-2 pr-3">
                    <span className={`badge ${c.estado === 'Verificado' ? 'tint-success' : c.estado === 'Observado' ? 'tint-danger' : 'tint-warning'}`}>
                      {ESTADOS.find((e) => e.valor === c.estado)?.nombre ?? c.estado}
                    </span>
                  </td>
                  <td className="py-2">
                    <div className="flex items-center justify-end gap-1">
                      <button onClick={() => ver(c)} className="btn-secondary !px-2" title="Vista previa">
                        <Eye size={15} />
                      </button>
                      <button onClick={() => descargar(c.id)} className="btn-secondary !px-2" title="Descargar archivo">
                        <Download size={15} />
                      </button>
                      <button
                        onClick={() => setComentario({ id: c.id, texto: c.comentarioRevision ?? '' })}
                        disabled={ocupado === c.id}
                        className={`btn-secondary !px-2 ${c.estado === 'Observado' ? '' : ''}`}
                        title={c.estado === 'Observado' ? 'Quitar observación (verificar)' : 'Marcar como observado'}
                      >
                        <EyeOff size={15} />
                      </button>
                      <button
                        onClick={() => revisar(c.id, true)}
                        disabled={ocupado === c.id || c.estado === 'Verificado'}
                        className="btn-primary !px-2"
                        title="Verificar"
                      >
                        <Check size={15} />
                      </button>
                    </div>
                    {comentario?.id === c.id && (
                      <div className="mt-2 flex items-center gap-2">
                        <input
                          autoFocus
                          value={comentario.texto}
                          onChange={(e) => setComentario({ id: c.id, texto: e.target.value })}
                          placeholder="Motivo de la observación..."
                          className="input"
                        />
                        <button onClick={() => revisar(c.id, false)} disabled={ocupado === c.id} className="btn-danger !px-3 !py-1.5">
                          Observar
                        </button>
                        <button onClick={() => setComentario(null)} className="btn-secondary !px-3 !py-1.5">Cancelar</button>
                      </div>
                    )}
                  </td>
                </tr>
              ))}
              {!paginados.length && (
                <tr>
                  <td colSpan={8} className="py-4 text-center text-ink-secondary">
                    Sin coincidencias con los filtros aplicados.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        ) : (
          <p className="py-4 text-sm text-ink-secondary">No hay certificados para mostrar.</p>
        )}

        {total > 0 && (
          <div className="mt-3 flex flex-wrap items-center justify-between gap-3 border-t border-soft pt-3 text-sm">
            <div className="flex items-center gap-2">
              <span className="text-ink-secondary">
                Mostrando {desde}–{hasta} de {total}
              </span>
              <select
                value={tamano}
                onChange={(e) => { setTamano(Number(e.target.value)); setPagina(1); }}
                className="input !w-auto !py-1"
              >
                {TAMANOS.map((t) => (
                  <option key={t} value={t}>{t} por página</option>
                ))}
              </select>
            </div>
            <div className="flex items-center gap-2">
              <button
                onClick={() => setPagina((p) => Math.max(1, p - 1))}
                disabled={paginaActual <= 1}
                className="btn-secondary !px-2 !py-1"
                title="Anterior"
              >
                <ChevronLeft size={14} />
              </button>
              <span className="text-ink-secondary">Página {paginaActual} de {totalPaginas}</span>
              <button
                onClick={() => setPagina((p) => Math.min(totalPaginas, p + 1))}
                disabled={paginaActual >= totalPaginas}
                className="btn-secondary !px-2 !py-1"
                title="Siguiente"
              >
                <ChevronRight size={14} />
              </button>
            </div>
          </div>
        )}
      </div>

      {viendo && <VisorArchivo archivo={viendo} onCerrar={() => setViendo(null)} />}
    </div>
  );
}
