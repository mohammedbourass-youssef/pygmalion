import React, { useEffect, useMemo, useState } from 'react'
import { Check, ChevronDown, CircleHelp, Clipboard, CloudDownload, Code2, CodeXml, Coffee, FileCode2, Folder, Github, Heart, Image, LoaderCircle, Play, RotateCcw, Smartphone, Sparkles, Zap } from 'lucide-react'
import { downloadSavedFile, generatePreview, getSupportedLanguages, validateClasses, type Language, type ValidationResult } from '../api/pygmalionApi'

const samples: Record<Language, string> = {
  JAVA: `import java.util.List;\n\npublic class Person {\n    private String firstName;\n    private String lastName;\n    private String email;\n    private String avatarUrl;\n    private List<Address> addresses;\n}\n\nclass Address {\n    private String street;\n    private String city;\n    private String country;\n    private String postalCode;\n}`,
  'C#': `using System.Collections.Generic;\n\npublic class Person {\n    public string FirstName { get; set; }\n    public string LastName { get; set; }\n    public string Email { get; set; }\n    public string AvatarUrl { get; set; }\n    public List<Address> Addresses { get; set; }\n}\n\npublic class Address {\n    public string Street { get; set; }\n    public string City { get; set; }\n    public string Country { get; set; }\n    public string PostalCode { get; set; }\n}`,
  PYTHON: `from typing import List\n\nclass Person:\n    first_name: str\n    last_name: str\n    email: str\n    avatar_url: str\n    addresses: List[Address]\n\nclass Address:\n    street: str\n    city: str\n    country: str\n    postal_code: str`,
  'C++': `#include <string>\n#include <vector>\n\nstruct Person {\n    std::string firstName;\n    std::string lastName;\n    std::string email;\n    std::string avatarUrl;\n    std::vector<Address> addresses;\n};\n\nstruct Address {\n    std::string street;\n    std::string city;\n    std::string country;\n    std::string postalCode;\n};`,
  GO: `package models\n\ntype Person struct {\n    FirstName string\n    LastName  string\n    Email     string\n    AvatarUrl string\n    Addresses []Address\n}\n\ntype Address struct {\n    Street     string\n    City       string\n    Country    string\n    PostalCode string\n}`,
  DART: `class Person {\n  String firstName;\n  String lastName;\n  String email;\n  String avatarUrl;\n  List<Address> addresses;\n}\n\nclass Address {\n  String street;\n  String city;\n  String country;\n  String postalCode;\n}`,
  TYPESCRIPT: `interface Person {\n  firstName: string;\n  lastName: string;\n  email: string;\n  avatarUrl: string;\n  addresses: Address[];\n}\n\ninterface Address {\n  street: string;\n  city: string;\n  country: string;\n  postalCode: string;\n}`,
  JAVASCRIPT: `class Person {\n  firstName;\n  lastName;\n  email;\n  avatarUrl;\n  addresses; // Address[]\n}\n\nclass Address {\n  street;\n  city;\n  country;\n  postalCode;\n}`,
}
const fallbackLanguages: Language[] = ['JAVA', 'C#', 'PYTHON', 'C++', 'GO', 'DART', 'TYPESCRIPT', 'JAVASCRIPT']
const donateUrl = import.meta.env.VITE_DONATE_URL || 'https://github.com/sponsors/mohammedbourass-youssef'

function highlightJson(source: string) {
  const parts = source.match(/"(?:\\.|[^"\\])*"|\b(?:true|false|null)\b|-?\d+(?:\.\d+)?|[{}[\],:]|\s+|./g) ?? []
  return parts.map((part, index) => {
    if (/^"/.test(part)) {
      const next = parts.slice(index + 1).find(token => token.trim())
      return <span className={next === ':' ? 'json-key' : 'json-string'} key={index}>{part}</span>
    }
    if (/^-?\d/.test(part)) return <span className="json-number" key={index}>{part}</span>
    if (/^(true|false|null)$/.test(part)) return <span className="json-literal" key={index}>{part}</span>
    return <React.Fragment key={index}>{part}</React.Fragment>
  })
}

export default function App() {
  const [language, setLanguage] = useState<Language>('JAVA')
  const [languages, setLanguages] = useState<Language[]>([])
  const [apiOnline, setApiOnline] = useState(false)
  const [code, setCode] = useState(samples.JAVA)
  const [count, setCount] = useState(5)
  const [nestedCount, setNestedCount] = useState(3)
  const [json, setJson] = useState('')
  const [filePath, setFilePath] = useState('')
  const [fileName, setFileName] = useState('')
  const [generatedLanguage, setGeneratedLanguage] = useState<Language | null>(null)
  const [busy, setBusy] = useState<'validate' | 'generate' | 'download' | null>(null)
  const [error, setError] = useState<{ message: string; missing?: string[] } | null>(null)
  const [status, setStatus] = useState<{ root: string; count: number; time: number } | null>(null)
  const [copied, setCopied] = useState<'json' | 'path' | null>(null)
  const [showGuide, setShowGuide] = useState(false)
  const [showPathHelp, setShowPathHelp] = useState(false)

  useEffect(() => {
    getSupportedLanguages().then(available => {
      const supported = available.length ? available : fallbackLanguages
      setLanguages(supported)
      setApiOnline(true)
      if (!supported.includes(language)) { setLanguage(supported[0]); setCode(samples[supported[0]]) }
    }).catch(() => { setLanguages(fallbackLanguages); setApiOnline(false) })
  }, [])

  const lines = useMemo(() => code.split('\n').length, [code])
  const chars = code.length
  const rootClass = useMemo(() => code.match(/(?:class|struct|interface|type)\s+(\w+)/)?.[1] ?? 'Root', [code])
  const run = async (action: 'validate' | 'generate') => {
    if (!code.trim()) { setError({ message: 'Add class definitions to continue.' }); return }
    setBusy(action); setError(null)
    const started = performance.now()
    try {
      const request = { language, code, count, nestedCount }
      if (action === 'validate') {
        try {
          const result: ValidationResult = await validateClasses({ language, code })
          if (result.missingClasses?.length) throw Object.assign(new Error(result.message ?? 'Please Fill All Classes'), { missingClasses: result.missingClasses })
          setStatus({ root: result.rootClassName ?? result.rootClass ?? rootClass, count, time: Math.round(performance.now() - started) })
          return
        } catch (validationError) {
          const ex = validationError as Error & { missingClasses?: string[] }
          if (ex.missingClasses) throw ex
          // The current backend has no validate route; its create endpoint runs the same parser and nested-class validation.
        }
      }
      const generated = await generatePreview(request)
      setJson(JSON.stringify(generated.data, null, 2))
      setFilePath(generated.filePath ?? '')
      setFileName(generated.fileName ?? '')
      setGeneratedLanguage(language)
      setStatus({ root: generated.rootClass ?? rootClass, count, time: Math.round(performance.now() - started) })
    } catch (e) {
      const ex = e as Error & { missingClasses?: string[] }
      setError({ message: ex.missingClasses?.length ? 'Please Fill All Classes' : ex.message || 'Something went wrong. Check your API connection.', missing: ex.missingClasses })
    } finally { setBusy(null) }
  }
  const copyText = async (value: string, kind: 'json' | 'path' = 'json') => {
    if (!value) return
    await navigator.clipboard.writeText(value)
    setCopied(kind)
    window.setTimeout(() => setCopied(null), 1800)
  }
  const download = async () => {
    if (!filePath || !fileName) return
    setBusy('download')
    try {
      const file = await downloadSavedFile(filePath, fileName)
      const objectUrl = URL.createObjectURL(file.blob)
      const anchor = document.createElement('a'); anchor.href = objectUrl; anchor.download = file.filename; anchor.click()
      URL.revokeObjectURL(objectUrl)
    } catch (e) { setError({ message: (e as Error).message }) }
    finally { setBusy(null) }
  }

  return <div className="app-shell">
    <header className="topbar"><a className="brand" href="#top"><img src="/pygmalion-banner.svg" alt="Pygmalion"/></a><div className="header-right"><div className={`connection-pill ${apiOnline ? 'online' : 'offline'}`}><span/>{apiOnline ? 'Backend connected' : 'Connecting to backend'}</div><button className="button button-quiet sample-button" onClick={() => setCode(samples[language])}><Sparkles size={17}/>Load Sample</button><a className="button donate-button" href={donateUrl} target="_blank" rel="noreferrer"><Heart size={17}/>Donate</a><button className={`button button-quiet guide-toggle ${showGuide ? 'selected' : ''}`} onClick={() => setShowGuide(!showGuide)}><CircleHelp size={17}/>How it works</button></div></header>
    <main className="main-wrap" id="top">
      {showGuide && <section className="guide-drawer"><div className="guide-step"><span>01</span><div><b>Select language</b><p>Choose from eight Tree-sitter grammars.</p></div></div><div className="guide-connector"/><div className="guide-step"><span>02</span><div><b>Paste all classes</b><p>Include every referenced nested type.</p></div></div><div className="guide-connector"/><div className="guide-step"><span>03</span><div><b>Synthesize & export</b><p>Preview realistic data, copy or download.</p></div></div></section>}
      <section className="hero"><div className="hero-copy"><div className="eyebrow"><Zap size={15}/> AST-TO-DATA SYNTHESIS ENGINE</div><h1>Make your schema <span>come alive.</span></h1><p>Turn class definitions into rich, realistic test data — all in one focused workspace.</p></div><div className="hero-orbit"><div className="orbit-ring ring-one"/><div className="orbit-ring ring-two"/><div className="orbit-core"><img src="/pygmalion-logo.svg" alt=""/></div><span className="orbit-node node-a"/><span className="orbit-node node-b"/><span className="orbit-node node-c"/></div></section>
      <div className="workspace">
        <section className="studio-column">
          <section className="card control-card"><div className="card-heading"><div><span className="section-kicker">CONFIGURATION</span><h2>Generation controls</h2></div><span className="soft-badge"><Code2 size={14}/>Tree-sitter</span></div>
            <label className="field language-field"><span className="field-label">Source language</span><div className="select-wrap"><select value={language} onChange={e => { const next = e.target.value as Language; setLanguage(next); setCode(samples[next]) }}>{languages.map(item => <option key={item} value={item}>{item}</option>)}</select><ChevronDown size={18}/></div></label>
            <div className="count-grid"><label className="field"><span className="field-label">Root records <span className="field-hint">count</span></span><input type="number" min="1" max="1000" value={count} onChange={e => setCount(Math.min(1000, Math.max(1, Number(e.target.value))))}/><div className="preset-row">{[5,25,100].map(n => <button className={count === n ? 'preset active' : 'preset'} key={n} onClick={() => setCount(n)}>{n}</button>)}</div></label><label className="field"><span className="field-label">Nested list size <span className="field-hint">nestedCount</span></span><input type="number" min="1" max="50" value={nestedCount} onChange={e => setNestedCount(Math.min(50, Math.max(1, Number(e.target.value))))}/><div className="preset-row">{[3,10,25].map(n => <button className={nestedCount === n ? 'preset active' : 'preset'} key={n} onClick={() => setNestedCount(n)}>{n}</button>)}</div></label></div>
          </section>
          <section className="card editor-card"><div className="card-heading editor-heading"><div><span className="section-kicker">YOUR SCHEMA</span><h2>Source classes</h2></div><div className="editor-tools"><span className="counter">{lines} lines <i/> {chars.toLocaleString()} chars</span><button className="text-button" onClick={() => setCode('')}><RotateCcw size={15}/>Clear</button></div></div>
            <div className="editor-shell"><div className="line-numbers" aria-hidden="true">{Array.from({length: lines}, (_, i) => <span key={i}>{i + 1}</span>)}</div><textarea spellCheck={false} value={code} onChange={e => setCode(e.target.value)} onKeyDown={e => { if (e.key === 'Tab') { e.preventDefault(); const t = e.currentTarget; const start = t.selectionStart; const end = t.selectionEnd; setCode(t.value.slice(0,start)+'  '+t.value.slice(end)); requestAnimationFrame(() => { t.selectionStart = t.selectionEnd = start+2 }) } }} aria-label="Source class definitions" placeholder="Paste class definitions here…"/></div>
            <div className="editor-bottom"><span><span className="editor-status-dot"/> {language} <i/> Indent: 2 spaces</span><span>Include referenced classes, e.g. <code>Address</code></span></div>
            {language === 'JAVASCRIPT' && <aside className="javascript-note"><div className="javascript-note-mark">JS</div><div className="javascript-note-content"><h3>Use ES2022 Class Field Syntax <span>Immediate test</span></h3><p>Make sure the Language dropdown is set to JAVASCRIPT and paste this model using direct class properties (without <code>constructor()</code>):</p><pre><code>{`class Person {\n  firstName;\n  lastName;\n  email;\n  avatarUrl;\n}`}</code></pre><button type="button" onClick={() => setCode(`class Person {\n  firstName;\n  lastName;\n  email;\n  avatarUrl;\n}`)}>Load this model</button></div></aside>}
          </section>
          <div className="action-row"><button className="button button-primary" disabled={!!busy} onClick={() => run('generate')}>{busy === 'generate' ? <LoaderCircle className="spin" size={19}/> : <Play size={18} fill="currentColor"/>}{busy === 'generate' ? 'Synthesizing data…' : 'Generate data'}<span className="action-arrow">↗</span></button><button className="button button-secondary" disabled={!!busy} onClick={() => run('validate')}>{busy === 'validate' ? <LoaderCircle className="spin" size={18}/> : <Check size={18}/>}Validate</button></div>
        </section>
        <section className="output-column"><section className="card output-status-card"><div className="card-heading"><div><span className="section-kicker">GENERATION STATUS</span><h2>Output & validation</h2></div><span className="soft-badge"><span className="status-dot"/>{status ? 'Ready' : 'Standing by'}</span></div>
          {error ? <div className="error-banner"><div className="error-sign">!</div><div><h3>{error.message}</h3><p>{error.missing?.length ? 'Add these referenced classes to your source:' : 'Review your class definitions and backend connection.'}</p>{error.missing?.length ? <div className="missing-pills">{error.missing.map(name => <span key={name}>{name}</span>)}</div> : null}</div></div> : status ? <div className="success-banner"><span className="success-mark"><Check size={18}/></span><div className="success-copy"><b>Data synthesized successfully</b><p><span className="root-chip">Root: {status.root}</span><span>{status.count} records</span><span>{status.time} ms</span></p></div></div> : <div className="idle-banner"><span className="idle-pulse"/>Ready when you are<span>Define a schema to get started</span></div>}
        </section>
        {filePath && <section className="path-card"><div className="path-icon"><Folder size={19}/></div><div className="path-content"><div className="path-main"><div className="path-copy"><span className="path-label">SAVED OUTPUT PATH</span><code title={filePath}>{filePath}</code></div><div className="path-actions"><button className="button path-copy-button" onClick={() => copyText(filePath, 'path')}>{copied === 'path' ? <Check size={16}/> : <Clipboard size={16}/>} {copied === 'path' ? 'Copied!' : 'Copy'}</button><button className={`path-help-button ${showPathHelp ? 'active' : ''}`} aria-label="What is this link?" aria-expanded={showPathHelp} onClick={() => setShowPathHelp(!showPathHelp)}>?</button></div></div>{showPathHelp && <div className="path-help"><div className="path-help-heading"><b>How to use this saved output</b><span className="language-chip">{generatedLanguage ?? language}</span></div><p>This is a ready-to-call REST API <code>GET</code> endpoint. Pygmalion generated and saved this dataset from your <strong>{generatedLanguage ?? language}</strong> class definitions. The language is already applied—the URL serves the JSON directly, with no request body or language parameter needed.</p><p>Call it from any frontend, script, or API client. Each request returns the same saved JSON dataset until you generate a new one.</p><pre><code>{`const response = await fetch("${filePath}");\nconst data = await response.json();`}</code></pre><span className="path-help-note">Use <code>GET</code> · Reusable URL · Same saved response on each call</span></div>}</div></section>}
        <section className="card json-card"><div className="json-toolbar"><div className="json-title"><span className="json-symbol">{`{ }`}</span><div><h2>Generated JSON</h2><span>{fileName || 'Preview appears after generation'}</span></div></div><div className="json-actions"><button className="button small-button" disabled={!json} onClick={() => copyText(json)}>{copied === 'json' ? <Check size={16}/> : <Clipboard size={16}/>} {copied === 'json' ? 'Copied!' : 'Copy JSON'}</button><button className="button small-button download-small" disabled={!filePath || !!busy} onClick={download}>{busy === 'download' ? <LoaderCircle className="spin" size={16}/> : <CloudDownload size={16}/>} Download {fileName || '.json'}</button></div></div>
          {json ? <pre className="json-view"><code>{highlightJson(json)}</code></pre> : <div className="empty-state"><div className="empty-logo"><img src="/pygmalion-logo.svg" alt="Pygmalion"/><Sparkles size={20}/></div><h3>Your generated data lives here</h3><p>Configure your schema, then synthesize to preview<br className="desktop-break"/> realistic, nested JSON data.</p><div className="empty-hint"><FileCode2 size={16}/> JSON preview <span/> Powered by Bogus</div></div>}
          <div className="json-footer"><span><span className="footer-green"/>{json ? 'JSON ready' : 'Waiting for output'} <i/> UTF-8</span><span>Pretty printed</span></div>
        </section>
        <div className="output-footnote"><span><Zap size={15}/> Tree-sitter parsing <i/> Bogus data generation</span><span>Realistic by design.</span></div>
        </section>
      </div>
      <section className="about-section" id="about">
        <div className="about-intro"><div className="about-copy"><span className="section-kicker">BUILT FOR THE MOMENT BEFORE THE BACKEND</span><h2>What is Pygmalion?</h2><p>Pygmalion is a free, open-source AST-to-Data synthesis engine that gives you an instant API with realistic data for any models you write. Paste your classes in Java, C#, Python, C++, Go, Dart, TypeScript, or JavaScript, configure your counts, and get structured JSON with smart field values, nested objects, and real image URLs.</p><div className="roadmap-pills"><span className="available-pill"><Check size={16}/>Available now: Instant GET endpoint <b>·</b> Free &amp; open source</span><span className="roadmap-pill"><Zap size={16}/>Coming next: Full CRUD API <b>·</b> POST, PUT, DELETE</span></div></div><div className="about-emblem"><img src="/pygmalion-logo.svg" alt=""/><span>AST <i>→</i> DATA</span></div></div>
        <div className="about-grid"><article className="about-card story-card"><div className="about-card-heading"><span className="about-icon"><Heart size={19}/></span><div><span className="section-kicker">THE STORY</span><h3>Why I built Pygmalion</h3></div></div><p>Every new frontend, mobile app, or prototype hits the same wall: the backend isn’t ready yet. Hand-writing mock JSON or configuring fake schemas steals time from building the product. Pygmalion turns the models you already have into realistic API data in one click—so you can keep shipping while the backend catches up.</p><p>Use the Java, C#, Python, TypeScript, Go, Dart, C++, or JavaScript definitions already in your project. No mock boilerplate. Just your models, alive with data.</p><a className="button about-donate" href={donateUrl} target="_blank" rel="noreferrer"><Coffee size={19}/>Support the project — Donate<Heart size={16}/></a></article>
          <article className="about-card usecases-card"><div className="about-card-heading"><span className="about-icon"><Sparkles size={19}/></span><div><span className="section-kicker">MADE FOR REAL WORKFLOWS</span><h3>Use cases &amp; scenarios</h3></div></div><div className="scenario-list"><div className="scenario"><span className="scenario-icon"><Smartphone size={19}/></span><div><h4>Frontend &amp; mobile prototyping</h4><p>Build React, Flutter, or Next.js UIs against rich nested JSON before the backend is built.</p></div></div><div className="scenario"><span className="scenario-icon"><Zap size={19}/></span><div><h4>Stress &amp; pagination testing</h4><p>Generate 100+ parent records with nested lists like 100 × 10 to exercise rendering and scroll performance.</p></div></div><div className="scenario"><span className="scenario-icon"><Image size={19}/></span><div><h4>Realistic client demos</h4><p>Populate profiles, products, and feeds with names, addresses, and avatars—not “Lorem ipsum”.</p></div></div><div className="scenario"><span className="scenario-icon"><CodeXml size={19}/></span><div><h4>Multi-language model testing</h4><p>Validate class relationships across 8 languages and export {`{Guid}.json`} datasets immediately.</p></div></div></div></article></div>
      </section>
      <footer className="page-footer"><span>PYGMALION <i/> AST-TO-DATA SYNTHESIS ENGINE</span><div className="footer-links"><a href="#about">About &amp; roadmap</a><a href="https://github.com" target="_blank" rel="noreferrer"><Github size={15}/> GitHub</a><a className="footer-donate" href={donateUrl} target="_blank" rel="noreferrer"><Heart size={15}/> Donate</a></div></footer>
    </main>
  </div>
}
