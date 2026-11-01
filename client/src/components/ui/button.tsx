import { Slot } from '@radix-ui/react-slot';
import { cva, type VariantProps } from 'class-variance-authority';
import { clsx } from 'clsx';
import { twMerge } from 'tailwind-merge';
import { useId, type ButtonHTMLAttributes } from 'react';
export const cn = (...inputs: Parameters<typeof clsx>) => twMerge(clsx(inputs));
const variants = cva('button', { variants: { variant: { default: 'button-primary', secondary: 'button-secondary', ghost: 'button-ghost', danger: 'button-danger' }, size: { default: '', sm: 'button-sm', icon: 'button-icon' } }, defaultVariants: { variant: 'default', size: 'default' } });
// A blocked action always says why: the reason is shown beside the button and announced with it.
export function Button({ className, variant, size, asChild = false, blockedReason, disabled, 'aria-describedby': describedBy, ...props }: ButtonHTMLAttributes<HTMLButtonElement> & VariantProps<typeof variants> & { asChild?: boolean; blockedReason?: string | false | null }) {
 const reasonId = useId();
 const Component = asChild ? Slot : 'button';
 const button = <Component className={cn(variants({ variant, size }), className)} disabled={disabled || !!blockedReason} {...props} aria-describedby={blockedReason ? clsx(describedBy, reasonId) : describedBy}/>;
 if (!blockedReason) return button;
 return <span className="blocked-action">{button}<span className="blocked-reason" id={reasonId}>{blockedReason}</span></span>;
}
// For blocked controls that are not a single Button (fieldsets, inputs, raw buttons): render beside them and point aria-describedby at its id.
export function BlockedNote({ id, reason, className }: { id?: string; reason?: string | false | null; className?: string }) {
 return reason ? <p className={cn('blocked-reason blocked-note', className)} id={id} role="note">{reason}</p> : null;
}
