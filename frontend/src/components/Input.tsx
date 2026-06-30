import React from 'react';
import { cn } from '../lib/utils';

interface InputProps extends React.InputHTMLAttributes<HTMLInputElement> {
  label?: string;
}

const base =
  'w-full h-14 rounded-md border border-m3-outline bg-m3-surface-container px-4 py-2 text-m3-on-surface placeholder:text-m3-on-surface-variant/60 transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-m3-primary focus-visible:border-m3-primary disabled:opacity-50';

export const Input: React.FC<InputProps> = ({ label, className, id, ...props }) => {
  const inputId = id || (label ? `field-${label.replace(/\s+/g, '-').toLowerCase()}` : undefined);
  const input = <input id={inputId} className={cn(base, className)} {...props} />;

  if (!label) return input;

  return (
    <div className="w-full">
      <label htmlFor={inputId} className="text-xs font-medium text-m3-on-surface-variant ml-1 mb-1 block">
        {label}
      </label>
      {input}
    </div>
  );
};
