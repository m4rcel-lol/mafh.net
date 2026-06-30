import React from 'react';
import { cn } from '../lib/utils';

// Verified badge — shield uses the site accent (m3-primary via currentColor),
// the checkmark uses the contrasting on-primary color.
export const VerifiedBadge: React.FC<{ className?: string; title?: string }> = ({ className, title = 'Verified' }) => (
  <svg
    viewBox="0 0 2039.9 2500"
    className={cn('inline-block h-5 w-5 shrink-0 text-m3-primary', className)}
    role="img"
    aria-label={title}
  >
    <title>{title}</title>
    <path d="m1991.4 503.9-942 1934.3-1001.9-1284.1z" fill="var(--color-m3-on-primary)" />
    <path
      d="m1019.9 0-1019.9 453.3v680c0 632.3 437.4 1223.9 1019.9 1366.7 588.2-143.9 1019.9-734.4 1019.9-1366.7v-680zm-226.6 1822.3-453.3-453.3 160.9-160.9 294.6 293.5 748-755.9 160.9 162.1z"
      fill="currentColor"
    />
  </svg>
);
