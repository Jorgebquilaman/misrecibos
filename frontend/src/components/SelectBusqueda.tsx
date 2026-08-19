import { useEffect, useMemo, useRef, useState } from 'react';
import { Check, ChevronDown, Search } from 'lucide-react';

export interface OpcionSelectBusqueda {
  id: string;
  etiqueta: string;
}

/** Combo con filtro por teclado: se escribe y filtra en vivo; flechas + Enter para elegir. */
export default function SelectBusqueda({
  opciones,
  valor,
  onChange,
  placeholder = 'Buscar...',
  opcionVacia,
  className = ''
}: {
  opciones: OpcionSelectBusqueda[];
  valor: string;
  onChange: (id: string) => void;
  placeholder?: string;
  /** Si se provee, agrega una primera opción con id "" (p. ej. "Todos" / "Para mí"). */
  opcionVacia?: string;
  className?: string;
}) {
  const [abierto, setAbierto] = useState(false);
  const [busqueda, setBusqueda] = useState('');
  const [indice, setIndice] = useState(-1);
  const contenedorRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  const seleccionada = opciones.find((o) => o.id === valor);

  const filtradas = useMemo(() => {
    const q = busqueda.trim().toLocaleLowerCase('es');
    if (!q) return opciones;
    return opciones.filter((o) => o.etiqueta.toLocaleLowerCase('es').includes(q));
  }, [opciones, busqueda]);

  useEffect(() => {
    if (!abierto) return;
    const cerrar = (e: MouseEvent) => {
      if (contenedorRef.current && !contenedorRef.current.contains(e.target as Node)) setAbierto(false);
    };
    document.addEventListener('mousedown', cerrar);
    return () => document.removeEventListener('mousedown', cerrar);
  }, [abierto]);

  useEffect(() => {
    if (abierto) {
      setBusqueda('');
      setIndice(-1);
      inputRef.current?.focus();
    }
  }, [abierto]);

  const elegir = (id: string) => {
    onChange(id);
    setAbierto(false);
  };

  return (
    <div ref={contenedorRef} className={`relative ${className}`}>
      <button
        type="button"
        onClick={() => setAbierto((v) => !v)}
        className="input flex w-full items-center justify-between gap-2 text-left"
      >
        <span className={`truncate ${seleccionada || opcionVacia ? '' : 'text-ink-muted'}`}>
          {seleccionada ? seleccionada.etiqueta : opcionVacia ?? placeholder}
        </span>
        <ChevronDown size={16} className="shrink-0 text-ink-muted" />
      </button>

      {abierto && (
        <div className="absolute z-30 mt-1 w-full rounded-[14px] border border-soft bg-surface p-2 shadow-lg">
          <div className="relative">
            <Search size={14} className="absolute left-2.5 top-1/2 -translate-y-1/2 text-ink-muted" />
            <input
              ref={inputRef}
              value={busqueda}
              onChange={(e) => {
                setBusqueda(e.target.value);
                setIndice(-1);
              }}
              onKeyDown={(e) => {
                if (e.key === 'ArrowDown') {
                  e.preventDefault();
                  setIndice((i) => Math.min(i + 1, filtradas.length - 1));
                } else if (e.key === 'ArrowUp') {
                  e.preventDefault();
                  setIndice((i) => Math.max(i - 1, 0));
                } else if (e.key === 'Enter') {
                  const opcion = indice >= 0 ? filtradas[indice] : filtradas[0];
                  if (opcion) elegir(opcion.id);
                } else if (e.key === 'Escape') {
                  setAbierto(false);
                }
              }}
              placeholder="Escribí para filtrar..."
              className="input !py-1.5 pl-8 text-sm"
            />
          </div>
          <ul className="mt-1 max-h-56 overflow-y-auto">
            {opcionVacia && (
              <li>
                <button
                  type="button"
                  onClick={() => elegir('')}
                  className={`flex w-full items-center justify-between rounded-lg px-3 py-1.5 text-left text-sm ${
                    valor === '' ? 'bg-surface-soft' : 'text-ink-muted'
                  }`}
                >
                  {opcionVacia}
                  {valor === '' && <Check size={14} className="text-accent-text" />}
                </button>
              </li>
            )}
            {filtradas.length === 0 && (
              <li className="px-3 py-2 text-sm text-ink-muted">Sin resultados</li>
            )}
            {filtradas.map((o, i) => (
              <li key={o.id}>
                <button
                  type="button"
                  onMouseEnter={() => setIndice(i)}
                  onClick={() => elegir(o.id)}
                  className={`flex w-full items-center justify-between rounded-lg px-3 py-1.5 text-left text-sm ${
                    i === indice ? 'bg-surface-soft' : ''
                  }`}
                >
                  {o.etiqueta}
                  {o.id === valor && <Check size={14} className="text-accent-text" />}
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}