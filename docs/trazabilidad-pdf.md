# Trazabilidad PDF — Skill

## Resumen
Skill para agregar una marca de trazabilidad discreta en **todas las páginas** de un PDF, sin rasterizar, preservando texto seleccionable, vectores e hipervínculos. Dos modos: **Morse** (puntos/rayas vectoriales) y **Código de Barras** (Code128 compacto). Genera ID único `DOC-XXXXXXXX`, calcula SHA-256 del original, y registra en `trace-registry.json`. Provee función de decodificación.

## Stack
- .NET 8, C#
- `PdfSharpCore 1.3.67` — overlay con `XGraphics.FromPdfPage(..., Append)` (copia páginas vía `PdfReader.Open(..., Import)` + `AddPage`, no rasteriza)
- `ZXing.Net 0.16.9` + `System.Drawing.Common 8.0.0` — Code128
- `QuestPDF` ya en proyecto (no usado para trazabilidad, solo para tests que generan PDFs de prueba)

## Estructura
```
src/PortalIUPA.Infrastructure/Pdf/Trazabilidad/
  Models.cs                  — enums Posicion/Codificacion, DTOs Resultado/Registro
  MorseEncoder.cs            — Encode/Decode + Dibujar (puntos Ø1.6pt, rayas 4.5×1.6pt)
  BarcodeEncoder.cs          — Generar Code128 70×14pt @300dpi + Decodificar
  PdfTrazabilidadService.cs  — IPdfTrazabilidadService (Add + TryDecode, hash, coordenadas, registro)

.agents/skills/trazabilidad-pdf/SKILL.md — definición del skill
docs/trazabilidad-pdf.md     — este documento
tests/PortalIUPA.Application.Tests/PdfTrazabilidadTests.cs — pruebas
```

## Dependencias
```xml
<PackageReference Include="PdfSharpCore" Version="1.3.67" />
<PackageReference Include="ZXing.Net" Version="0.16.9" />
<PackageReference Include="System.Drawing.Common" Version="8.0.0" />
```
En Linux/macOS con `System.Drawing.Common` se requiere `libgdiplus`:
```bash
# macOS
brew install mono-libgdiplus
# Debian/Ubuntu
sudo apt-get install libgdiplus
```
Alternativa sin `System.Drawing`: migrar a `ZXing.Net.Bindings.SkiaSharp` (no incluido por defecto).

## Instalación
El servicio se registra en `DependencyInjection.cs`:
```csharp
services.AddScoped<IPdfTrazabilidadService, PdfTrazabilidadService>();
```
No requiere configuración adicional.

## Uso

### Inyección (recomendado)
```csharp
var svc = provider.GetRequiredService<IPdfTrazabilidadService>();
var r = await svc.AddTraceabilityAsync(
    inputPdf: "contrato.pdf",
    userId: "legajo421",
    position: TrazabilidadPosicion.FooterCenter,
    encoding: TrazabilidadCodificacion.Barcode);
// r.OutputPdf == "contrato-trazabilidad.pdf", r.TraceId == "DOC-8F42A7C9"
```

### Parámetros completos
```csharp
Task<TrazabilidadResultado> AddTraceabilityAsync(
    string inputPdf,                 // existente
    string? outputPdf = null,        // null => [nombre]-trazabilidad.pdf
    string userId,                   // requerido
    string? traceId = null,          // null => DOC-XXXXXXXX (RandomNumberGenerator, 8 hex)
    TrazabilidadPosicion position = FooterRight,
    TrazabilidadCodificacion encoding = Morse,
    DateTime? fechaHora = null,      // null => Now
    string? registroJsonPath = null, // null => ./trace-registry.json junto al output
    CancellationToken ct = default);
```

Posiciones: `HeaderLeft|HeaderCenter|HeaderRight|FooterLeft|FooterCenter|FooterRight` (20pt X, 14pt Y, clamp a página).

### CLI conceptual
```
add-pdf-traceability(
    inputPdf = "documento.pdf",
    outputPdf = "documento-trazabilidad.pdf",
    userId = "usuario123",
    traceId = "DOC-8F42A7C91B",
    position = "footer-right",
    encodingType = "morse"
)
```

## Registro JSON
`trace-registry.json` (append, `WriteIndented`):
```json
[
  {
    "TraceId": "DOC-8F42A7C91B",
    "UserId": "legajo421",
    "InputFile": "contrato.pdf",
    "InputHashSha256": "e3b0c44...",
    "FechaHora": "2026-08-27T14:30:00-03:00",
    "Encoding": "morse",
    "Position": "footer-right",
    "OutputFile": "contrato-trazabilidad.pdf"
  }
]
```
El `InputHashSha256` permite verificar que el original no fue alterado. El `traceId` es opaco; los datos sensibles quedan solo en el registro.

## Decodificación
```csharp
var dec = await svc.TryDecodeAsync("contrato-trazabilidad.pdf");
if (dec != null) Console.WriteLine($"ID {dec.TraceId} via {dec.Encoding}");

// Desde escaneo
var dec2 = await svc.TryDecodeAsync("escaneo.jpg");
// Para barcode usa ZXing directamente sobre el bitmap; para morse vectorial puro
// la decodificación automática desde escaneo requiere pre-procesado, se resuelve vía registro.
```

Limitaciones de decodificación:
- **Barcode**: 100% decodificable desde PDF (imagen embebida) o foto/escaneo con ZXing si la impresión es nítida.
- **Morse**: al ser vectores (`XGraphics.DrawEllipse/DrawRectangle`), la decodificación automática desde PDF requeriría parsear el contenido vectorial; actualmente se resuelve vía `trace-registry.json` y `MorseEncoder.Decode` para validación manual. Para escaneo, se requiere binarización y detección de puntos/rayas (no incluido en v1).

## Seguridad
- No se codifica `Nombre=Juan` legible; solo `DOC-XXXXXXXX`.
- El PDF solo porta el ID; el mapeo `ID → usuario/hash/fecha` está en el JSON (acceso restringido).
- El `traceId` se valida: `DOC-` + 4–16 hex.

## Preservación de calidad
- `PdfReader.Open(..., Import)` + `AddPage`: no se rasteriza, se copia el contenido original (texto, vectores, imágenes, hipervínculos donde PdfSharpCore los soporta).
- La marca se añade con `XGraphics.FromPdfPage(page, Append)` como capa superior, sin modificar el contenido subyacente.

## Pruebas
`tests/PortalIUPA.Application.Tests/PdfTrazabilidadTests.cs`:
- Morse encode/decode roundtrip
- Barcode generate/decode roundtrip
- Generación preserva texto seleccionable (verifica que el texto original sigue extraíble)
- Todas las páginas reciben marca (cuenta páginas)
- Hash SHA-256 y nombre de salida
- Registro JSON append
- Manejo de errores (archivo inexistente, traceId inválido, posición inválida)

Ejecutar:
```bash
dotnet test tests/PortalIUPA.Application.Tests --filter PdfTrazabilidad
```

## Ejemplo E2E
```csharp
// 1. Generar
var r1 = await svc.AddTraceabilityAsync("a.pdf", userId: "u1", encoding: TrazabilidadCodificacion.Morse, position: TrazabilidadPosicion.HeaderCenter);
var r2 = await svc.AddTraceabilityAsync("b.pdf", userId: "u2", traceId: "DOC-ABCDEF12", encoding: TrazabilidadCodificacion.Barcode, position: TrazabilidadPosicion.FooterRight);
// 2. Verificar
Debug.Assert(File.Exists("a-trazabilidad.pdf"));
Debug.Assert(File.ReadAllText("trace-registry.json").Contains("DOC-"));
// 3. Decodificar
var d = await svc.TryDecodeAsync("b-trazabilidad.pdf");
Debug.Assert(d?.TraceId == "DOC-ABCDEF12");
```
