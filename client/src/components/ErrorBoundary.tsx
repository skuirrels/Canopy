import { Component } from 'react';
import type { ReactNode } from 'react';
import { Sprout, RefreshCw } from 'lucide-react';
import { Button } from './ui/button';

export class ErrorBoundary extends Component<{ children: ReactNode }, { failed: boolean }> {
 state = { failed: false };
 static getDerivedStateFromError() { return { failed: true }; }
 render() {
  if (this.state.failed) return <main className="full-state" style={{ minHeight: '100dvh' }}><Sprout size={35}/><h1>Something got a little tangled.</h1><p>Your saved projects are still in the database.<br/>Reload to reopen your workspace. Unsaved task drafts are kept in this browser.</p><Button onClick={() => location.reload()}><RefreshCw size={15}/>Reopen workspace</Button></main>;
  return this.props.children;
 }
}
