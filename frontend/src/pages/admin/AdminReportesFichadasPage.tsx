import { useEffect, useMemo, useState } from 'react';
import { FileSpreadsheet, FileText } from 'lucide-react';
import { adminApi } from '../../api';
import type { EmpleadoDto } from '../../types';
import { descargarBlob } from '../../utils';

const TIPOS = [
  { valor: 'individual', nombre: 'Individual (por empleado)', desc: 'Día por día: entradas, salidas, horas, tardanzas y anomalías.' },
  { valor: 'resumen', nombre: 'Resumen institucional', desc: 'Días trabajados, horas totales y promedio por empleado.' },
  { valor: 'ausencias', nombre: 'Ausencias', desc: 'Quiénes no marcaron cada día laborable del período.' },
  { valor: 'tardanzas', nombre: 'Tardanzas', desc: 'Entradas posteriores a las 08:30 con el retraso en minutos.' }
];

export default function AdminReportesFichadasPage() {
  const [tipo, setTipo] = useState('individual');
  const [legajo, setLegajo] = useState<string>('');
  const [desde, setDesde] = useState(() => new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString().slice(0, 10));
  const [hasta, setHasta] = useState(() => new Date().toISOString().slice(0, 10));
  const [empleados, setEmpleados] = useState<EmpleadoDto[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [ocupado, setOcupado] = useState(false);

  useEffect(() => {
    const cargarTodos = async () => {
      let todos: EmpleadoDto[] = [];
      for (let pagina = 1; ; pagina++) {
        const r = await adminApi.empleados({ pagina, tamano: 200 });
        todos = todos.concat(r.items);
        if (todos.length >= r.total) break;
      }
      setEmpleados(todos);
    };
    cargarTodos().catch(() => {});
  }, []);

  const empleadosOrdenados = useMemo(
    () => [...empleados].sort((a, b) => `${a.apellido} ${a.nombre}`.localeCompare(`${b.apellido} ${b.nombre}`)),
    [empleados]
  );

  const descargar = async (formato: 'pdf' | 'xlsx') => {
    setError(null);
    setOcupado(true);
    try {
      const blob = await adminApi.reporteFichadas(
        tipo,
        tipo === 'individual' && legajo ? Number(legajo) : null,
        desde,
        hasta,
        formato
      );
      descargarBlob(blob, `ReporteFichadas_${formato === 'pdf' ? 'PDF' : 'XLSX'}_${desde}_${hasta}.${formato}`);
    } catch (e: any) {
      setError(e.response?.data?.error ?? 'No se pudo generar el reporte.');
    } finally {
      setOcupado(false);
    }
  };

  const tipoInfo = TIPOS.find((t) => t.valor === tipo)!;

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Reportes de fichadas</h1>
        <p className="text-sm text-ink-secondary">Generá informes a partir de las marcas del reloj biométrico (en vivo).</p>
      </div>

      {error && <p className="rounded-lg tint-danger px-3 py-2 text-sm">{error}</p>}

      <div className="card space-y-4">
        <div>
          <label className="label">Tipo de reporte</label>
          <select value={tipo} onChange={(e) => setTipo(e.target.value)} className="input">
            {TIPOS.map((t) => (
              <option key={t.valor} value={t.valor}>{t.nombre}</option>
            ))}
          </select>
          <p className="mt-1 text-xs text-ink-secondary">{tipoInfo.desc}</p>
        </div>

        {tipo === 'individual' && (
          <div>
            <label className="label">Empleado</label>
            <select value={legajo} onChange={(e) => setLegajo(e.target.value)} className="input">
              <option value="">Seleccioná un empleado...</option>
              {empleadosOrdenados.map((e) => (
                <option key={e.id} value={e.legajo}>
                  {e.legajo} · {e.apellido}, {e.nombre}
                </option>
              ))}
            </select>
          </div>
        )}

        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="label">Desde</label>
            <input type="date" value={desde} onChange={(e) => setDesde(e.target.value)} className="input" />
          </div>
          <div>
            <label className="label">Hasta</label>
            <input type="date" value={hasta} onChange={(e) => setHasta(e.target.value)} className="input" />
          </div>
        </div>

        <div className="flex gap-2">
          <button
            onClick={() => descargar('pdf')}
            disabled={ocupado || (tipo === 'individual' && !legajo)}
            className="btn-primary flex-1"
          >
            <FileText size={16} /> {ocupado ? 'Generando...' : 'Descargar PDF'}
          </button>
          <button
            onClick={() => descargar('xlsx')}
            disabled={ocupado || (tipo === 'individual' && !legajo)}
            className="btn-secondary flex-1"
          >
            <FileSpreadsheet size={16} /> {ocupado ? 'Generando...' : 'Descargar Excel'}
          </button>
        </div>
      </div>
    </div>
  );
}