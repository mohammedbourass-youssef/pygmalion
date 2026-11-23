import React from 'react'
import { createRoot } from 'react-dom/client'
import App from './ui/App'
import './ui/styles.css'
import './ui/about.css'
import './ui/path.css'
import './ui/javascript-note.css'

createRoot(document.getElementById('root')!).render(<React.StrictMode><App /></React.StrictMode>)
