import { useEffect, useRef, useState } from 'react';
import { Link2, Table2 } from 'lucide-react';
import { reportesApi } from '../api';
import type { TablaListaDto } from '../types';

const ANCHO_CAJA = 210;
const ALTO_CAB = 26;
const ALTO_FILA = 20;
const MAX_FILAS_MOSTRADAS = 18;

interface TablaEnLienzo {
  esquema: string;
  tabla: string;
  columnas: string[];
  seleccionadas: Set<string>;
  expandida: boolean;
  x: number;
  y: number;
}

interface Relacion {
  id: string;
  a: string;
  colA: string;
  b: string;
  colB: string;
  tipo: 'inner' | 'left';
}

export default function AsistenteVisual({ conexion, onGenerar, onCerrar }: {
  conexion: string;
  onGenerar: (sql: string) => void;
  onCerrar: () => void;
}) {
  const [tablas, setTablas] = useState<TablaListaDto[]>([]);
  const [busqueda, setBusqueda] = useState('');
  const [enLienzo, setEnLienzo] = useState<TablaEnLienzo[]>([]);
  const [relaciones, setRelaciones] = useState<Relacion[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [expandidos, setExpandidos] = useState<Set<string>>(new Set());

  useEffect(() => {
    reportesApi.tablas(conexion).then(setTablas).catch(() => setError('No se pudieron listar las tablas de la conexión.'));
  }, [conexion]);

  const agregarTabla = async (esquema: string, tabla: string) => {
    const clave = `${esquema}.${tabla}`;
    if (enLienzo.some((t) => clave === `${t.esquema}.${t.tabla}`)) return;

    try {
      const det = await reportesApi.tablaDetalle(conexion, esquema, tabla);
      const nueva: TablaEnLienzo = {
        esquema, tabla,
        columnas: det.columnas.map((c) => c.nombre),
        seleccionadas: new Set<string>(),
        expandida: false,
        x: 24 + enLienzo.length * 240,
        y: 24 + (enLienzo.length % 2) * 180
      };
      setEnLienzo((prev) => [...prev, nueva]);

      // Relaciones automáticas por FK contra tablas ya presentes
      const nuevas: Relacion[] = [];
      for (const rel of det.relaciones) {
        const [esqExt, tabExt] = rel.tablaExterna.split('.');
        const destino = enLienzo.find((t) => t.esquema === esqExt && t.tabla === tabExt);
        if (destino) {
          nuevas.push({
            id: `${clave}.${rel.colLocal}-${rel.tablaExterna}.${rel.colExterna}-${Date.now()}`,
            a: clave, colA: rel.colLocal,
            b: `${esqExt}.${tabExt}`, colB: rel.colExterna,
            tipo: 'inner'
          });
        }
      }
      if (nuevas.length > 0) setRelaciones((prev) => [...prev, ...nuevas]);
    } catch {
      setError(`No se pudo leer la tabla ${clave}.`);
    }
  };

  const iniciarArrastre = (e: React.PointerEvent, clave: string) => {
    e.stopPropagation();
    const caja = enLienzo.find((t) => `${t.esquema}.${t.tabla}` === clave);
    if (!caja) return;
    arrastreRef.current = { key: clave, dx: e.clientX - caja.x, dy: e.clientY - caja.y };
    const mover = (ev: PointerEvent) => {
      if (!arrastreRef.current) return;
      const { key: k, dx, dy } = arrastreRef.current;
      setEnLienzo((prev) => prev.map((t) => (key(t) === k ? { ...t, x: Math.max(0, ev.clientX - dx), y: Math.max(0, ev.clientY - dy) } : t)));
    };
    const soltar = () => {
      arrastreRef.current = null;
      window.removeEventListener('pointermove', mover);
      window.removeEventListener('pointerup', soltar);
    };
    window.addEventListener('pointermove', mover);
    window.addEventListener('pointerup', soltar);
  };

  const arrastreRef = useRef<{ key: string; dx: number; dy: number } | null>(null);
  const key = (t: TablaEnLienzo) => `${t.esquema}.${t.tabla}`;

  const generarSql = () => {
    const columnas = enLienzo.flatMap((t) =>
      Array.from(t.seleccionadas).map((c) => `"${t.esquema}"."${t.tabla}"."${c}"`)
    );
    if (enLienzo.length === 0 || columnas.length === 0) {
      setError('Agregá al menos una tabla y tildá los campos que querés ver.');
      return;
    }
    const primera = enLienzo[0];
    const partes = [`select ${columnas.join(', ')}`, `from "${primera.esquema}"."${primera.tabla}"`];
    for (const rel of relaciones) {
      const a = enLienzo.find((t) => key(t) === rel.a);
      const b = enLienzo.find((t) => key(t) === rel.b);
      if (!a || !b) continue;
      partes.push(
        `${rel.tipo === 'left' ? 'left join' : 'join'} "${b.esquema}"."${b.tabla}" ` +
        `on "${b.esquema}"."${b.tabla}"."${rel.colB}" = "${a.esquema}"."${a.tabla}"."${rel.colA}"`
      );
    }
    onGenerar(partes.join('\n'));
  };

  const posicionAncla = (tablaKey: string, col: string, lado: 'izq' | 'der'): { x: number; y: number } => {
    const t = enLienzo.find((x) => key(x) === tablaKey);
    if (!t) return { x: 0, y: 0 };
    const idx = Math.max(0, t.columnas.indexOf(col));
    const fila = Math.min(idx, MAX_FILAS_MOSTRADAS);
    return { x: lado === 'der' ? t.x + ANCHO_CAJA : t.x, y: t.y + ALTO_CAB + fila * ALTO_FILA + ALTO_FILA / 2 };
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4" onClick={onCerrar}>
      <div className="flex max-h-[90vh] w-full max-w-5xl flex-col overflow-hidden rounded-xl border border-soft bg-base shadow-2xl" onClick={(e) => e.stopPropagation()}>
        <div className="flex items-center justify-between border-b border-soft p-3">
          <h3 className="flex items-center gap-2 font-semibold"><Table2 size={16} /> Asistente visual de consulta — conexión: {conexion}</h3>
          <button onClick={onCerrar} className="btn-secondary !px-2 !py-1 text-xs">Cerrar</button>
        </div>



        <div className="flex min-h-0 flex-1 border-b border-soft">
        {/* Árbol de esquemas */}
        <div className="w-56 shrink-0 overflow-auto border-r border-soft p-2 text-xs">
          <input
            value={busqueda}
            onChange={(e) => setBusqueda(e.target.value)}
            placeholder="Buscar tabla…"
            className="input mb-2 w-full text-xs"
          />
          {(() => {
            const q = busqueda.trim().toLowerCase();
            const porEsquema = new Map<string, TablaListaDto[]>();
            for (const t of tablas) {
              if (q && !`${t.esquema}.${t.tabla}`.toLowerCase().includes(q)) continue;
              if (!porEsquema.has(t.esquema)) porEsquema.set(t.esquema, []);
              porEsquema.get(t.esquema)!.push(t);
            }
            return Array.from(porEsquema.entries()).map(([esq, lista]) => (
              <div key={esq} className="mb-1">
                <button
                  onClick={() => setExpandidos((prev) => {
                    const next = new Set(prev);
                    next.has(esq) ? next.delete(esq) : next.add(esq);
                    return next;
                  })}
                  className="flex w-full items-center gap-1 rounded px-1 py-1 text-left font-semibold hover:bg-black/[0.04]"
                >
                  {expandidos.has(esq) || q ? '▾' : '▸'} <span className="truncate">{esq}</span>
                  <span className="ml-auto text-[10px] text-ink-muted">{lista.length}</span>
                </button>
                {(expandidos.has(esq) || q) && (
                  <ul className="ml-3 border-l border-soft pl-2">
                    {lista.map((t) => (
                      <li key={t.tabla}>
                        <button
                          onClick={() => void agregarTabla(t.esquema, t.tabla)}
                          className="flex w-full items-center gap-1 truncate rounded px-1 py-0.5 text-left hover:bg-black/[0.04]"
                          title={`${t.esquema}.${t.tabla}`}
                        >
                          <Table2 size={11} className="shrink-0 text-ink-muted" />
                          <span className="truncate">{t.tabla}</span>
                          {enLienzo.some((x) => `${x.esquema}.${x.tabla}` === `${t.esquema}.${t.tabla}`) && (
                            <span className="ml-auto text-[9px] text-success">✓</span>
                          )}
                        </button>
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            ));
          })()}
        </div>

        {/* Lienzo */}
        <div className="relative min-h-[380px] flex-1 overflow-auto bg-black/[0.04]" style={{ backgroundImage: 'radial-gradient(circle, rgba(128,128,128,0.25) 1px, transparent 1px)', backgroundSize: '18px 18px' }}>
          <svg className="pointer-events-none absolute left-0 top-0 h-full w-full">
            {relaciones.map((rel) => {
              const p1 = posicionAncla(rel.a, rel.colA, 'der');
              const p2 = posicionAncla(rel.b, rel.colB, 'izq');
              const mx = (p1.x + p2.x) / 2;
              return (
                <path
                  key={rel.id}
                  d={`M ${p1.x} ${p1.y} C ${mx} ${p1.y}, ${mx} ${p2.y}, ${p2.x} ${p2.y}`}
                  stroke={rel.tipo === 'left' ? '#ca8a04' : '#2563eb'}
                  strokeWidth={1.6}
                  fill="none"
                />
              );
            })}
          </svg>
          {enLienzo.length === 0 && (
            <p className="p-6 text-sm text-ink-secondary">Elegí un esquema a la izquierda y hacé clic en las tablas para agregarlas.</p>
          )}
          {enLienzo.map((t) => (
            <div
              key={key(t)}
              className="absolute rounded border border-soft bg-base shadow"
              style={{ left: t.x, top: t.y, width: ANCHO_CAJA }}
            >
              <div
                onPointerDown={(e) => iniciarArrastre(e, key(t))}
                className="flex cursor-grab items-center gap-1 rounded-t border-b border-soft bg-black/[0.05] px-2 py-0.5 text-xs font-semibold"
              >
                <Table2 size={12} /> {t.tabla}
                <button
                  onClick={() => setEnLienzo((prev) => prev.filter((x) => key(x) !== key(t)))}
                  className="ml-auto text-ink-muted hover:text-danger" title="Quitar tabla"
                >×</button>
              </div>
              <ul className="max-h-[280px] overflow-auto text-xs">
                {(t.expandida ? t.columnas : t.columnas.slice(0, MAX_FILAS_MOSTRADAS)).map((c) => (
                  <li key={c} className="flex items-center gap-1 px-2 py-0.5 hover:bg-black/[0.03]">
                    <input
                      type="checkbox"
                      checked={t.seleccionadas.has(c)}
                      onChange={(e) => setEnLienzo((prev) => prev.map((x) => {
                        if (key(x) !== key(t)) return x;
                        const sel = new Set(x.seleccionadas);
                        e.target.checked ? sel.add(c) : sel.delete(c);
                        return { ...x, seleccionadas: sel };
                      }))}
                    />
                    <span className="truncate" title={c}>{c}</span>
                  </li>
                ))}
                {!t.expandida && t.columnas.length > MAX_FILAS_MOSTRADAS && (
                  <li>
                    <button
                      onClick={() => setEnLienzo((prev) => prev.map((x) => (key(x) === key(t) ? { ...x, expandida: true } : x)))}
                      className="w-full px-2 py-0.5 text-left text-[10px] text-blue-600 hover:underline"
                    >
                      … mostrar las {t.columnas.length - MAX_FILAS_MOSTRADAS} columnas restantes
                    </button>
                  </li>
                )}
                {t.expandida && t.columnas.length > MAX_FILAS_MOSTRADAS && (
                  <li>
                    <button
                      onClick={() => setEnLienzo((prev) => prev.map((x) => (key(x) === key(t) ? { ...x, expandida: false } : x)))}
                      className="w-full px-2 py-0.5 text-left text-[10px] text-ink-muted hover:underline"
                    >
                      ↑ mostrar menos
                    </button>
                  </li>
                )}
              </ul>
            </div>
          ))}
        </div>
        </div>

        {/* Relaciones */}
        <div className="border-t border-soft p-3 text-xs">
          <p className="mb-1 flex items-center gap-1 font-semibold"><Link2 size={13} /> Relaciones (JOIN)</p>
          {relaciones.length === 0 && <p className="text-ink-muted">Se detectan solas por claves foráneas al agregar tablas, o crealas acá.</p>}
          <div className="space-y-1">
            {relaciones.map((rel) => (
              <div key={rel.id} className="flex flex-wrap items-center gap-1">
                <select value={rel.a} onChange={(e) => setRelaciones((prev) => prev.map((r) => r.id === rel.id ? { ...r, a: e.target.value, colA: '' } : r))} className="input !py-0.5 text-xs">
                  {enLienzo.map((t) => <option key={key(t)} value={key(t)}>{t.tabla}</option>)}
                </select>
                <select value={rel.colA} onChange={(e) => setRelaciones((prev) => prev.map((r) => r.id === rel.id ? { ...r, colA: e.target.value } : r))} className="input !py-0.5 text-xs">
                  <option value="">columna…</option>
                  {enLienzo.find((t) => key(t) === rel.a)?.columnas.map((c) => <option key={c} value={c}>{c}</option>)}
                </select>
                <span className="font-mono">=</span>
                <select value={rel.b} onChange={(e) => setRelaciones((prev) => prev.map((r) => r.id === rel.id ? { ...r, b: e.target.value, colB: '' } : r))} className="input !py-0.5 text-xs">
                  {enLienzo.map((t) => <option key={key(t)} value={key(t)}>{t.tabla}</option>)}
                </select>
                <select value={rel.colB} onChange={(e) => setRelaciones((prev) => prev.map((r) => r.id === rel.id ? { ...r, colB: e.target.value } : r))} className="input !py-0.5 text-xs">
                  <option value="">columna…</option>
                  {enLienzo.find((t) => key(t) === rel.b)?.columnas.map((c) => <option key={c} value={c}>{c}</option>)}
                </select>
                <select value={rel.tipo} onChange={(e) => setRelaciones((prev) => prev.map((r) => r.id === rel.id ? { ...r, tipo: e.target.value as 'inner' | 'left' } : r))} className="input !py-0.5 text-xs">
                  <option value="inner">INNER</option>
                  <option value="left">LEFT</option>
                </select>
                <button onClick={() => setRelaciones((prev) => prev.filter((r) => r.id !== rel.id))} className="text-ink-muted hover:text-danger">×</button>
              </div>
            ))}
          </div>
          {enLienzo.length >= 2 && (
            <button
              onClick={() => setRelaciones((prev) => [...prev, { id: `rel-${Date.now()}`, a: key(enLienzo[0]), colA: '', b: key(enLienzo[1]), colB: '', tipo: 'inner' }])}
              className="mt-1 text-blue-600 hover:underline"
            >+ relación manual</button>
          )}
        </div>

        {error && <p className="px-3 pb-1 text-xs text-danger">{error}</p>}

        <div className="flex items-center justify-end gap-2 border-t border-soft p-3">
          <button onClick={onCerrar} className="btn-secondary text-sm">Cancelar</button>
          <button
            onClick={generarSql}
            className="btn-primary text-sm"
            disabled={enLienzo.length === 0}
          >Generar SQL</button>
        </div>
      </div>
    </div>
  );
}
