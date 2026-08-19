import { useEffect, useState } from 'react';
import { Download } from 'lucide-react';
import { certificadosApi } from '../api';
import type { CertificadoDto } from '../types';
import { descargarBlob, formatFecha, formatFechaHora } from '../utils';

export default function CertificadosPage() {
  const [mios, setMios] = useState<CertificadoDto[]>([]);
  const [tipo, setTipo] = useState('ConstanciaTrabajo');
  const [desde, setDesde] = useState('');
  const [hasta, setHasta] = useState('');
  const [destino, setDestino] = useState('');
  const [mensaje, setMensaje] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);
  const [descargando, setDescargando] = useState<string | null>(null);

  const cargar = () => certificadosApi.mios().then(setMios).catch(() => {});

  useEffect(() => { cargar(); }, []);

  const solicitar = async () => {
    if (!desde || !hasta) {
      setError('Completá el período del certificado.');
      return;
    }
    setEnviando(true);
    setError(null);
    try {
      await certificadosApi.solicitar({ tipo, desde, hasta, destino: destino || undefined });
      setMensaje(`Certificado solicitado. Lo podés descargar cuando esté generado.`);
      setDesde('');
      setHasta('');
      setDestino('');
      cargar();
    } catch (e: any) {
      setError(e.response?.data?.error ?? 'No se pudo solicitar el certificado.');
    } finally {
      setEnviando(false);
    }
  };

  const descargar = async (id: string) => {
    setDescargando(id);
    try {
      const blob = await certificadosApi.descargar(id);
      const c = mios.find((x) => x.id === id);
      descargarBlob(blob, `Certificado_${c?.tipo ?? 'laboral'}_${c?.desde ?? ''}.pdf`);
    } catch (e: any) {
      setError(e.response?.data?.error ?? 'No se pudo descargar el certificado.');
    } finally {
      setDescargando(null);
    }
  };

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Certificados laborales</h1>
        <p className="text-sm text-ink-secondary">Constancias de trabajo y certificaciones de servicios en PDF.</p>
      </div>

      {mensaje && <p className="rounded-lg tint-success px-3 py-2 text-sm">{mensaje}</p>}
      {error && <p className="rounded-lg tint-danger px-3 py-2 text-sm">{error}</p>}

      <div className="card">
        <h2 className="mb-3 font-semibold">Solicitar certificado</h2>
        <div className="grid gap-3 sm:grid-cols-2">
          <div>
            <label className="label">Tipo</label>
            <select value={tipo} onChange={(e) => setTipo(e.target.value)} className="input">
              <option value="ConstanciaTrabajo">Constancia de trabajo</option>
              <option value="CertificacionServicios">Certificación de servicios</option>
            </select>
          </div>
          <div>
            <label className="label">Presentar ante (opcional)</label>
            <input value={destino} onChange={(e) => setDestino(e.target.value)} className="input" placeholder="Ej: Banco Patagonia" />
          </div>
          <div>
            <label className="label">Período desde</label>
            <input type="date" value={desde} onChange={(e) => setDesde(e.target.value)} className="input" />
          </div>
          <div>
            <label className="label">Período hasta</label>
            <input type="date" value={hasta} onChange={(e) => setHasta(e.target.value)} className="input" />
          </div>
        </div>
        <button onClick={solicitar} disabled={enviando} className="btn-primary mt-4">
          {enviando ? 'Generando...' : 'Solicitar certificado'}
        </button>
      </div>

      <div className="card">
        <h2 className="mb-3 font-semibold">Mis certificados</h2>
        {mios.length > 0 ? (
          <div className="space-y-2">
            {mios.map((c) => (
              <div key={c.id} className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-soft p-3">
                <div>
                  <p className="font-medium">
                    {c.tipo === 'ConstanciaTrabajo' ? 'Constancia de trabajo' : 'Certificación de servicios'}
                  </p>
                  <p className="text-sm text-ink-secondary">
                    {formatFecha(c.desde)} → {formatFecha(c.hasta)}
                    {c.destino ? ` · ${c.destino}` : ''} · solicitado {formatFechaHora(c.fechaSolicitud)}
                  </p>
                </div>
                <div className="flex items-center gap-2">
                  {c.estado === 'Generado' ? (
                    <button onClick={() => descargar(c.id)} disabled={descargando === c.id} className="btn-primary">
                      <Download size={16} /> PDF
                    </button>
                  ) : (
                    <span className="badge tint-warning">En generación</span>
                  )}
                </div>
              </div>
            ))}
          </div>
        ) : (
          <p className="text-sm text-ink-secondary">No solicitaste certificados todavía.</p>
        )}
      </div>
    </div>
  );
}