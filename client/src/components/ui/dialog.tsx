import * as DialogPrimitive from '@radix-ui/react-dialog';
import { X } from 'lucide-react';
import type { ReactNode } from 'react';
export function Dialog({ open, onOpenChange, title, description, children, wide = false, className = '' }: { open: boolean; onOpenChange: (open: boolean) => void; title: string; description?: string; children: ReactNode; wide?: boolean; className?: string }) {
 return <DialogPrimitive.Root open={open} onOpenChange={onOpenChange}><DialogPrimitive.Portal><DialogPrimitive.Overlay className="dialog-overlay"/><DialogPrimitive.Content className={`dialog-content ${wide ? 'dialog-wide' : ''} ${className}`} aria-describedby={description ? undefined : undefined}><DialogPrimitive.Title>{title}</DialogPrimitive.Title><DialogPrimitive.Description>{description || 'Manage your workspace.'}</DialogPrimitive.Description><DialogPrimitive.Close className="icon-button dialog-close" aria-label="Close dialog"><X size={18}/></DialogPrimitive.Close>{children}</DialogPrimitive.Content></DialogPrimitive.Portal></DialogPrimitive.Root>;
}
