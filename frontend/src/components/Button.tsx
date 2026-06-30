import React from 'react';
import { cn } from '../lib/utils';

type Variant = 'filled' | 'tonal' | 'text' | 'outlined' | 'fab';
type Size = 'sm' | 'md' | 'lg';

interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant;
  size?: Size;
  icon?: React.ReactNode;
}

const variantClasses: Record<Variant, string> = {
  filled: 'bg-m3-primary text-m3-on-primary hover:shadow-lg',
  tonal: 'bg-m3-secondary-container text-m3-on-secondary-container hover:shadow-md',
  text: 'bg-transparent text-m3-primary hover:bg-m3-primary/10',
  outlined: 'bg-transparent text-m3-primary border border-m3-outline hover:bg-m3-primary/10',
  fab: 'bg-m3-primary-container text-m3-on-primary-container rounded-2xl shadow-lg w-14 h-14',
};

const sizeClasses: Record<Size, string> = {
  sm: 'h-9 px-4 text-sm',
  md: 'h-11 px-6 text-sm',
  lg: 'h-12 px-8 text-base',
};

export const Button: React.FC<ButtonProps> = ({ variant = 'filled', size = 'md', icon, className, children, type = 'button', ...props }) => {
  const isFab = variant === 'fab';
  return (
    <button
      type={type}
      className={cn(
        'inline-flex items-center justify-center gap-2 rounded-full font-medium transition-all m3-ripple select-none disabled:opacity-50 disabled:pointer-events-none focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-m3-primary focus-visible:ring-offset-2 focus-visible:ring-offset-m3-surface',
        variantClasses[variant],
        !isFab && sizeClasses[size],
        className,
      )}
      {...props}
    >
      {icon}
      {children}
    </button>
  );
};
