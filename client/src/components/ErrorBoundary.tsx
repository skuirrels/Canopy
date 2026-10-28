import { Component } from 'react';
import type { ReactNode } from 'react';
import { Sprout, RefreshCw } from 'lucide-react';
import { Button } from './ui/button';

export class ErrorBoundary extends Component<{ children: ReactNode }, { error: string | null }> {
 state: { error: string | null } = { error: null };
 static getDerivedStateFromError(error: unknown) { return { error: error instanceof Error ? error.message : String(error) }; }
 render() {
  if (this.state.error !== null) {
   const assetFailure = /Failed to fetch dynamically imported module|Importing a module script failed|error loading dynamically imported module|Unable to preload CSS|Loading (?:CSS )?chunk .+ failed/i.test(this.state.error);
   return <main className="full-state" style={{ minHeight: '100dvh' }}><Sprout size={35}/><h1>{assetFailure ? 'A part of Canopy could not load.' : 'Something got a little tangled.'}</h1>{assetFailure && <p>This can happen when Canopy is updated while this tab is open, or its connection is interrupted.</p>}<p>Your saved projects are still in the database.<br/>Reload to reopen your workspace. Unsaved task drafts are kept in this browser.</p><Button onClick={() => location.reload()}><RefreshCw size={15}/>Reopen workspace</Button><details className="workspace-error-details"><summary>Error details</summary><pre>{this.state.error}</pre></details></main>;
  }
  return this.props.children;
 }
}
