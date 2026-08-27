import { useEffect, useState } from 'react';
import { X } from 'lucide-react';

export interface ArchivoParaVer {
  blob: Blob;
  nombre: string;
}

const esImagen = (nombre: string) => /\.(jpg|jpeg|png)$/i.test(nombre);

export default function VisorArchivo({ archivo, onCerrar }: { archivo: ArchivoParaVer; onCerrar: () => void }) {
  const [url, setUrl] = useState<string | null>(null);

  useEffect(() => {
    const objectUrl = URL.createObjectURL(archivo.blob);
    setUrl(objectUrl);
    return () => URL.revokeObjectURL(objectUrl);
  }, [archivo]);

  if (!url) return null;

  return (
    <div className="fixed inset-0 z-50 flex flex-col bg-black/70 p-4" onClick={onCerrar}>
      <div
        className="mx-auto flex h-full w-full max-w-4xl flex-col overflow-hidden rounded-card bg-surface shadow-card"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-center justify-between border-b border-soft px-4 py-2">
          <p className="truncate text-sm font-medium text-ink-primary">{archivo.nombre}</p>
          <div className="flex items-center gap-2">
            <a href={url} download={archivo.nombre} className="btn-secondary !px-3 !py-1.5 text-sm">
              Descargar
            </a>
            <button onClick={onCerrar} className="flex h-8 w-8 items-center justify-center rounded-pill bg-surface-soft text-ink-secondary hover:text-ink-primary">
              <X size={18} />
            </button>
          </div>
        </div>
        <div className="min-h-0 flex-1 bg-surface-alt">
          {esImagen(archivo.nombre) ? (
            <div className="flex h-full items-center justify-center p-4">
              <img src={url} alt={archivo.nombre} className="max-h-full max-w-full object-contain" />
            </div>
          ) : (
            <object data={url} type="application/pdf" className="h-full w-full">
              <p className="p-4 text-sm text-ink-secondary">
                Tu navegador no puede mostrar el PDF.
                <a href={url} download={archivo.nombre} className="text-accent-text underline"> Descargalo</a>.
              </p>
            </object>
          )}
        </div>
      </div>
    </div>
  );
}
