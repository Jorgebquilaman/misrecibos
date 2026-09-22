import { useEffect, useMemo, useState } from 'react';
import { Binary, ChevronLeft, ChevronRight, FileUp, ScanLine, Search } from 'lucide-react';
import { trazabilidadApi } from '../../api';
import type { TrazabilidadRegistroDto } from '../../types';
import { formatFechaHora } from '../../utils';

const TAMANOS = [10, 25, 50];

const ETIQUETA_ENCODING: Record<string, string> = {
  morse: 'Morse',
  barcode: 'Código de barras'
};

function Detalle({ registro }: { registro: TrazabilidadRegistroDto }) {
  return (
    <div className="card grid gap-4 sm:grid-cols-2">
      <div>
        <p className="text-[11px] font-medium uppercase tracking-wide text-ink-muted">TraceId (código Morse)</p>
        <p className="font-mono text-lg font-semibold text-ink-primary">{registro.traceId}</p>
      </div>
      <div>
        <p className="text-[11px] font-medium uppercase tracking-wide text-ink-muted">Legajo / usuario</p>
        <p className="font-medium text-ink-primary">{registro.userId}</p>
      </div>
      <div>
        <p className="text-[11px] font-medium uppercase tracking-wide text-ink-muted">Fecha y hora</p>
        <p className="text-ink-primary">{formatFechaHora(registro.fechaHora)}</p>
      </div>
      <div>
        <p className="text-[11px] font-medium uppercase tracking-wide text-ink-muted">Codificación / posición</p>
        <p className="text-ink-primary">
          {ETIQUETA_ENCODING[registro.encoding] ?? registro.encoding} · <span className="font-mono">{registro.position}</span>
        </p>
      </div>
      <div>
        <p className="text-[11px] font-medium uppercase tracking-wide text-ink-muted">Archivo de entrada</p>
        <p className="break-all text-ink-primary">{registro.inputFile}</p>
      </div>
      <div>
        <p className="text-[11px] font-medium uppercase tracking-wide text-ink-muted">Archivo de salida</p>
        <p className="break-all text-ink-primary">{registro.outputFile}</p>
      </div>
      <div className="sm:col-span-2">
        <p className="text-[11px] font-medium uppercase tracking-wide text-ink-muted">Hash SHA-256 (entrada)</p>
        <p className="break-all font-mono text-xs text-ink-secondary">{registro.inputHashSha256}</p>
      </div>
    </div>
  );
}

export default function AdminTrazabilidadPage() {
  const [traceId, setTraceId] = useState('');
  const [archivo, setArchivo] = useState<File | null>(null);
  const [morse, setMorse] = useState('');
  const [consultando, setConsultando] = useState(false);
  const [archivoSubiendo, setArchivoSubiendo] = useState(false);
  const [morseProcesando, setMorseProcesando] = useState(false);
  const [registro, setRegistro] = useState<TrazabilidadRegistroDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [mensaje, setMensaje] = useState<string | null>(null);

  const [historial, setHistorial] = useState<TrazabilidadRegistroDto[]>([]);
  const [historialError, setHistorialError] = useState<string | null>(null);
  const [texto, setTexto] = useState('');
  const [pagina, setPagina] = useState(1);
  const [tamano, setTamano] = useState(25);

  const cargarHistorial = () =>
    trazabilidadApi
      .historial()
      .then((h) => { setHistorial(h); setHistorialError(null); })
      .catch(() => setHistorialError('No se pudo cargar el historial.'));

  useEffect(() => {
    cargarHistorial();
  }, []);

  const consultarTraceId = async () => {
    const valor = traceId.trim();
    if (!valor) return;
    setConsultando(true);
    setError(null);
    setMensaje(null);
    try {
      const r = await trazabilidadApi.consultarTraceId(valor);
      setRegistro(r);
    } catch (e: any) {
      setRegistro(null);
      setError(e.response?.data?.error ?? 'No se pudo consultar el traceId.');
    } finally {
      setConsultando(false);
    }
  };

  const decodificar = async () => {
    if (!archivo) return;
    setArchivoSubiendo(true);
    setError(null);
    setMensaje(null);
    try {
      const r = await trazabilidadApi.decodificar(archivo);
      setRegistro(r);
    } catch (e: any) {
      setRegistro(null);
      setError(e.response?.data?.error ?? 'No se pudo decodificar el archivo.');
    } finally {
      setArchivoSubiendo(false);
    }
  };

  const decodificarMorse = async () => {
    if (!morse.trim()) return;
    setMorseProcesando(true);
    setError(null);
    setMensaje(null);
    try {
      const r = await trazabilidadApi.decodificarMorse(morse);
      setRegistro(r);
    } catch (e: any) {
      setRegistro(null);
      setError(e.response?.data?.error ?? 'No se pudo decodificar el código Morse.');
    } finally {
      setMorseProcesando(false);
    }
  };

  const filtrados = useMemo(() => {
    const q = texto.trim().toLowerCase();
    if (!q) return historial;
    return historial.filter((r) =>
      `${r.traceId} ${r.userId} ${r.inputFile} ${r.outputFile}`.toLowerCase().includes(q)
    );
  }, [historial, texto]);

  const total = filtrados.length;
  const totalPaginas = Math.max(1, Math.ceil(total / tamano));
  const paginaActual = Math.min(pagina, totalPaginas);
  const paginados = filtrados.slice((paginaActual - 1) * tamano, paginaActual * tamano);
  const desde = total === 0 ? 0 : (paginaActual - 1) * tamano + 1;
  const hasta = Math.min(paginaActual * tamano, total);

  return (
    <div className="mx-auto max-w-6xl space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Trazabilidad de PDFs</h1>
        <p className="text-sm text-ink-secondary">
          Consultá la información de un PDF marcado con el código Morse mediante su traceId (DOC-XXXXXXXX) o subiendo el PDF.
        </p>
      </div>

      {error && <p className="rounded-lg tint-danger px-3 py-2 text-sm">{error}</p>}
      {mensaje && <p className="rounded-lg tint-success px-3 py-2 text-sm">{mensaje}</p>}

      <div className="grid gap-4 lg:grid-cols-3">
        {/* Consultar por traceId */}
        <div className="card space-y-3">
          <h2 className="flex items-center gap-2 font-semibold">
            <Search size={18} /> Consultar por código Morse (traceId)
          </h2>
          <p className="text-sm text-ink-secondary">
            Pegá el traceId <span className="font-mono">DOC-XXXXXXXX</span> que aparece dibujado en la cabecera del PDF.
          </p>
          <div className="flex gap-2">
            <input
              value={traceId}
              onChange={(e) => setTraceId(e.target.value.toUpperCase())}
              onKeyDown={(e) => e.key === 'Enter' && consultarTraceId()}
              placeholder="DOC-8F42A7C9"
              className="input font-mono"
            />
            <button onClick={consultarTraceId} disabled={consultando || !traceId.trim()} className="btn-primary whitespace-nowrap">
              {consultando ? 'Buscando...' : 'Consultar'}
            </button>
          </div>
        </div>

        {/* Subir archivo */}
        <div className="card space-y-3">
          <h2 className="flex items-center gap-2 font-semibold">
            <FileUp size={18} /> Subir PDF marcado
          </h2>
          <p className="text-sm text-ink-secondary">
            Subí el PDF ya descargado (o su escaneo) para obtener su información de trazabilidad.
          </p>
          <div className="flex gap-2">
            <input
              type="file"
              accept=".pdf,.jpg,.jpeg,.png"
              onChange={(e) => setArchivo(e.target.files?.[0] ?? null)}
              className="input flex-1 file:mr-3 file:rounded-pill file:border-0 file:bg-surface-alt file:px-3 file:py-1 file:text-sm"
            />
            <button onClick={decodificar} disabled={archivoSubiendo || !archivo} className="btn-primary whitespace-nowrap">
              {archivoSubiendo ? 'Procesando...' : 'Decodificar'}
            </button>
          </div>
        </div>

        {/* Ingresar código Morse */}
        <div className="card space-y-3">
          <h2 className="flex items-center gap-2 font-semibold">
            <Binary size={18} /> Ingresar código Morse
          </h2>
          <p className="text-sm text-ink-secondary">
            Escribí los puntos y rayas visibles en el PDF, separando cada letra con un espacio (o <span className="font-mono">/</span>).
            Por ejemplo: <span className="font-mono">-.. --- -.-. -....-</span>
          </p>
          <div className="flex gap-2">
            <input
              value={morse}
              onChange={(e) => setMorse(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && decodificarMorse()}
              placeholder="-.. --- -.-. -....- 8 4 2"
              className="input flex-1 font-mono"
            />
            <button onClick={decodificarMorse} disabled={morseProcesando || !morse.trim()} className="btn-primary whitespace-nowrap">
              {morseProcesando ? 'Decodificando...' : 'Decodificar'}
            </button>
          </div>
        </div>
      </div>

      {registro && (
        <div className="space-y-2">
          <h2 className="flex items-center gap-2 font-semibold">
            <ScanLine size={18} /> Resultado de la consulta
          </h2>
          <Detalle registro={registro} />
        </div>
      )}

      {/* Historial */}
      <div className="card space-y-3">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h2 className="font-semibold">Historial de trazabilidad</h2>
          <input
            value={texto}
            onChange={(e) => { setTexto(e.target.value); setPagina(1); }}
            placeholder="Buscar por traceId, legajo o archivo..."
            className="input !w-auto flex-1 !max-w-sm"
          />
        </div>

        {historialError && <p className="rounded-lg tint-danger px-3 py-2 text-sm">{historialError}</p>}

        {!historialError && (
          <>
            {paginados.length > 0 ? (
              <div className="overflow-x-auto">
                <table className="w-full text-left text-sm">
                  <thead>
                    <tr className="border-b text-ink-secondary">
                      <th className="py-2 pr-3">TraceId</th>
                      <th className="py-2 pr-3">Legajo</th>
                      <th className="py-2 pr-3">Archivo</th>
                      <th className="py-2 pr-3">Codificación</th>
                      <th className="py-2 pr-3">Posición</th>
                      <th className="py-2">Fecha</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-soft">
                    {paginados.map((r) => (
                      <tr key={r.traceId + r.fechaHora} className="cursor-pointer hover:bg-surface-soft" onClick={() => setRegistro(r)}>
                        <td className="py-2 pr-3 font-mono text-accent-text">{r.traceId}</td>
                        <td className="py-2 pr-3">{r.userId}</td>
                        <td className="py-2 pr-3 break-all">{r.inputFile}</td>
                        <td className="py-2 pr-3">{ETIQUETA_ENCODING[r.encoding] ?? r.encoding}</td>
                        <td className="py-2 pr-3 font-mono">{r.position}</td>
                        <td className="py-2 whitespace-nowrap">{formatFechaHora(r.fechaHora)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : (
              <p className="py-4 text-sm text-ink-secondary">Todavía no hay registros de trazabilidad.</p>
            )}

            {total > 0 && (
              <div className="flex flex-wrap items-center justify-between gap-3 border-t border-soft pt-3 text-sm">
                <div className="flex items-center gap-2">
                  <span className="text-ink-secondary">Mostrando {desde}–{hasta} de {total}</span>
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
          </>
        )}
      </div>
    </div>
  );
}
