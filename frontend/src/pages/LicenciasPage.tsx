import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { ArrowRight, FileUp, X } from 'lucide-react';
import { adjuntosApi, licenciasApi } from '../api';
import type { ConsumoTipoLicenciaDto, SolicitudLicenciaDto, TipoLicenciaDto } from '../types';
import { COLOR_ESTADO, ETIQUETA_ESTADO, formatFecha, mesActual } from '../utils';

export default function LicenciasPage() {
  const [tipos, setTipos] = useState<TipoLicenciaDto[]>([]);
  const [consumo, setConsumo] = useState<ConsumoTipoLicenciaDto[]>([]);
  const [mias, setMias] = useState<SolicitudLicenciaDto[]>([]);
  const [mensaje, setMensaje] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const cargar = () => {
    licenciasApi.tipos().then(setTipos).catch(() => {});
    licenciasApi.consumo(mesActual().anio, mesActual().mes).then(setConsumo).catch(() => {});
    licenciasApi.misSolicitudes().then(setMias).catch(() => {});
  };

  useEffect(() => { cargar(); }, []);

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">Licencias</h1>
          <p className="text-sm text-ink-secondary">Solicitá licencias y seguí el estado de aprobación.</p>
        </div>
        <Link to="/pendientes" className="btn-secondary hidden sm:flex">
          Aprobar pendientes <ArrowRight size={16} />
        </Link>
      </div>

      {mensaje && <p className="rounded-lg tint-success px-3 py-2 text-sm">{mensaje}</p>}
      {error && <p className="rounded-lg tint-danger px-3 py-2 text-sm">{error}</p>}

      <div className="grid gap-6 lg:grid-cols-5">
        <div className="card lg:col-span-3">
          <h2 className="mb-3 font-semibold">Nueva solicitud</h2>
          <SolicitarForm
            tipos={tipos}
            onOk={(m) => {
              setMensaje(m);
              setError(null);
              cargar();
            }}
            onError={setError}
          />
        </div>

        <div className="card lg:col-span-2">
          <h2 className="mb-3 font-semibold">Consumo del mes actual</h2>
          <ul className="space-y-3">
            {consumo.map((c) => (
              <li key={c.tipoLicenciaId} className="flex items-center justify-between text-sm">
                <span className="truncate pr-2">{c.tipoLicenciaNombre}</span>
                <span className="shrink-0 text-ink-secondary">
                  {c.consumidosMes}/{c.limiteMensual ?? c.consumidosAnio}/{c.limiteAnual ?? '∞'}
                </span>
              </li>
            ))}
            {consumo.length === 0 && <p className="text-sm text-ink-secondary">Sin tipos de licencia activos.</p>}
          </ul>
        </div>
      </div>

      <div className="card">
        <h2 className="mb-3 font-semibold">Mis solicitudes</h2>
        {mias.length > 0 ? (
          <div className="space-y-2">
            {mias.map((s) => (
              <div key={s.id} className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-soft p-3">
                <div>
                  <p className="font-medium">
                    {s.tipoLicenciaNombre} <span className="text-ink-muted">· {s.dias} días</span>
                  </p>
                  <p className="text-sm text-ink-secondary">
                    {formatFecha(s.fechaInicio)} → {formatFecha(s.fechaFin)}
                  </p>
                </div>
                <div className="flex items-center gap-2">
                  <span className={`badge ${COLOR_ESTADO[s.estado] ?? 'bg-surface-soft text-ink-secondary'}`}>
                    {ETIQUETA_ESTADO[s.estado] ?? s.estado}
                  </span>
                  {s.estado === 'EnEspera' && (
                    <button
                      onClick={async () => {
                        await licenciasApi.cancelar(s.id);
                        cargar();
                      }}
                      className="btn-secondary !px-2 !py-1 text-xs"
                    >
                      <X size={14} /> Cancelar
                    </button>
                  )}
                </div>
              </div>
            ))}
          </div>
        ) : (
          <p className="text-sm text-ink-secondary">Todavía no solicitaste ninguna licencia.</p>
        )}
      </div>
    </div>
  );
}

function SolicitarForm({
  tipos,
  onOk,
  onError
}: {
  tipos: TipoLicenciaDto[];
  onOk: (m: string) => void;
  onError: (e: string) => void;
}) {
  const [tipoId, setTipoId] = useState('');
  const [inicio, setInicio] = useState('');
  const [fin, setFin] = useState('');
  const [asunto, setAsunto] = useState('');
  const [motivo, setMotivo] = useState('');
  const [adjuntoId, setAdjuntoId] = useState<string | null>(null);
  const [adjuntoNombre, setAdjuntoNombre] = useState<string | null>(null);
  const [subiendo, setSubiendo] = useState(false);
  const [enviando, setEnviando] = useState(false);

  const tipo = tipos.find((t) => t.id === tipoId);

  const subirAdjunto = async (archivo: File) => {
    setSubiendo(true);
    try {
      const r = await adjuntosApi.subir(archivo);
      setAdjuntoId(r.id);
      setAdjuntoNombre(archivo.name);
    } catch (e: any) {
      onError(e.response?.data?.error ?? 'No se pudo subir el adjunto.');
    } finally {
      setSubiendo(false);
    }
  };

  const enviar = async () => {
    if (!tipoId || !inicio || !fin) {
      onError('Completá el tipo de licencia y las fechas.');
      return;
    }
    setEnviando(true);
    try {
      const r = await licenciasApi.solicitar({
        tipoLicenciaId: tipoId,
        fechaInicio: inicio,
        fechaFin: fin,
        asunto: asunto || undefined,
        motivo: motivo || undefined,
        adjuntoId
      });
      setTipoId('');
      setInicio('');
      setFin('');
      setAsunto('');
      setMotivo('');
      setAdjuntoId(null);
      setAdjuntoNombre(null);
      onOk(`Solicitud registrada en estado "${ETIQUETA_ESTADO[r.estado] ?? r.estado}".`);
    } catch (e: any) {
      onError(e.response?.data?.error ?? 'No se pudo registrar la solicitud.');
    } finally {
      setEnviando(false);
    }
  };

  return (
    <div className="space-y-3">
      <div>
        <label className="label">Tipo de licencia</label>
        <select value={tipoId} onChange={(e) => setTipoId(e.target.value)} className="input">
          <option value="">Seleccioná un tipo...</option>
          {tipos.map((t) => (
            <option key={t.id} value={t.id}>
              {t.nombre}
            </option>
          ))}
        </select>
      </div>
      <div className="grid grid-cols-2 gap-3">
        <div>
          <label className="label">Desde</label>
          <input type="date" value={inicio} onChange={(e) => setInicio(e.target.value)} className="input" />
        </div>
        <div>
          <label className="label">Hasta</label>
          <input type="date" value={fin} onChange={(e) => setFin(e.target.value)} className="input" />
        </div>
      </div>
      <div>
        <label className="label">Asunto</label>
        <input value={asunto} onChange={(e) => setAsunto(e.target.value)} className="input" placeholder="Ej: Consulta médica" />
      </div>
      <div>
        <label className="label">Motivo</label>
        <textarea value={motivo} onChange={(e) => setMotivo(e.target.value)} className="input" rows={2} />
      </div>

      <div>
        <label className="label">Adjunto {tipo?.requiereAdjunto && <span className="text-danger">(obligatorio)</span>}</label>
        <label className="flex cursor-pointer items-center justify-center gap-2 rounded-lg border-2 border-dashed border-soft p-3 text-sm text-ink-secondary hover:border-accent/60 hover:text-accent-text">
          <FileUp size={16} />
          {adjuntoNombre ?? 'Certificado médico / archivo (PDF, JPG, PNG · máx 10 MB)'}
          <input
            type="file"
            className="hidden"
            accept=".pdf,.jpg,.jpeg,.png"
            disabled={subiendo}
            onChange={(e) => {
              const f = e.target.files?.[0];
              if (f) subirAdjunto(f);
            }}
          />
        </label>
        {adjuntoId && (
          <button onClick={() => { setAdjuntoId(null); setAdjuntoNombre(null); }} className="mt-1 text-xs text-danger">
            Quitar adjunto
          </button>
        )}
      </div>

      <button onClick={enviar} disabled={enviando || subiendo} className="btn-primary w-full">
        {enviando ? 'Enviando...' : 'Solicitar licencia'}
      </button>
      <p className="text-xs text-ink-muted">
        {tipo?.niveles.length === 0
          ? 'Este tipo se aprueba automáticamente.'
          : `Requiere aprobación de: ${tipo?.niveles.map((n) => n.rolRequerido).join(' → ') ?? ''}`}
      </p>
    </div>
  );
}