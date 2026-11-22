export type Language = 'JAVA' | 'C#' | 'PYTHON' | 'C++' | 'GO' | 'DART' | 'TYPESCRIPT' | 'JAVASCRIPT'
export interface SourceRequest { language: Language; code: string }
export interface GenerationRequest extends SourceRequest { count: number; nestedCount: number }
export interface ValidationResult { classes?: string[]; detectedClasses?: string[]; rootClass?: string; rootClassName?: string; missingClasses?: string[]; message?: string }
export interface GenerateResponse { rootClass?: string; filePath?: string; fileName?: string; data: unknown }

const base = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '')
// Keep route mapping here: these defaults match the ASP.NET API's existing routes.
const routes = { validate: '/api/v1/Pygmalion/validate', generate: '/api/v1/Pygmalion/create', file: (id: string) => `/api/v1/Pygmalion/${id}` }
const url = (path: string) => `${base}${path}`
async function readError(response: Response) {
  const data = await response.json().catch(() => null)
  const error = new Error(data?.message ?? `Request failed (${response.status})`) as Error & { missingClasses?: string[] }
  error.missingClasses = data?.missingClasses
  throw error
}
export async function validateClasses(request: SourceRequest): Promise<ValidationResult> {
  const response = await fetch(url(routes.validate), { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(request) })
  if (!response.ok) await readError(response)
  return response.json()
}
export async function getSupportedLanguages(): Promise<Language[]> {
  const response = await fetch(url('/api/v1/Pygmalion/languages'))
  if (!response.ok) await readError(response)
  const values: unknown = await response.json()
  if (!Array.isArray(values)) throw new Error('The API returned an invalid language list.')
  return values.filter((value): value is Language => typeof value === 'string' && ['JAVA', 'C#', 'PYTHON', 'C++', 'GO', 'DART', 'TYPESCRIPT', 'JAVASCRIPT'].includes(value))
}
async function createFile(request: GenerationRequest) {
  const response = await fetch(url(routes.generate), { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ language: request.language, code: request.code, numberOfRoots: request.count, countNested: request.nestedCount }) })
  if (!response.ok) await readError(response)
  // ASP.NET Ok(fileInternetPath) serializes the generated file URL as a JSON string.
  const body = await response.text()
  let link: string
  try { link = JSON.parse(body) as string } catch { link = body.trim().replace(/^"|"$/g, '') }
  if (!/^https?:\/\//.test(link)) throw new Error('The API generated the file but did not return a valid download URL.')
  const parsed = new URL(link)
  const id = parsed.pathname.split('/').filter(Boolean).pop() ?? ''
  // In local development send the returned link through Vite's same-origin proxy (avoids browser CORS).
  const fileUrl = base ? `${base}${parsed.pathname}` : parsed.pathname
  return { id, fileUrl, filePath: link, fileName: `${id || 'generated-data'}.json` }
}
export async function generatePreview(request: GenerationRequest): Promise<GenerateResponse> {
  // Current backend exposes create+save, then GET by GUID for JSON. A dedicated preview route can be substituted here.
  const { fileUrl, filePath, fileName } = await createFile(request)
  const response = await fetch(fileUrl)
  if (!response.ok) await readError(response)
  return { data: await response.json(), filePath, fileName }
}
export async function downloadSavedFile(filePath: string, fileName: string) {
  const parsed = new URL(filePath)
  const fileUrl = base ? `${base}${parsed.pathname}` : parsed.pathname
  const response = await fetch(fileUrl)
  if (!response.ok) await readError(response)
  return { blob: await response.blob(), filename: fileName }
}
