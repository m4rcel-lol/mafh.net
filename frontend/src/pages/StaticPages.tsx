import React from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from '../components/Button';

const Page: React.FC<{ title: string; children: React.ReactNode }> = ({ title, children }) => (
  <div className="max-w-3xl mx-auto py-10 animate-in fade-in duration-300">
    <h1 className="text-3xl font-bold mb-6">{title}</h1>
    <div className="space-y-4 text-m3-on-surface-variant leading-relaxed">{children}</div>
  </div>
);

export const Terms = () => (
  <Page title="Terms of Service">
    <p>These terms govern your use of this service. By creating an account or uploading files you agree to use the service lawfully and to respect the rights of others.</p>
    <p>This text is an operational template and not legal advice. Operators should review it with qualified counsel before launch.</p>
  </Page>
);

export const Privacy = () => (
  <Page title="Privacy Policy">
    <p>We store the account details and files you provide so the service can function. Files are kept on the operator's own infrastructure and are not sold to third parties.</p>
    <p>This text is an operational template and not legal advice.</p>
  </Page>
);

export const Rules = () => (
  <Page title="Community Rules">
    <p>Do not upload illegal content, malware, or material you do not have the rights to share. Mark sensitive content as NSFW. Abuse may result in removal of content or suspension.</p>
  </Page>
);

export const Dmca = () => (
  <Page title="DMCA Policy">
    <p>If you believe content hosted here infringes your copyright, contact the operator with the details required by the DMCA and the content will be reviewed.</p>
    <p>This text is an operational template and not legal advice.</p>
  </Page>
);

export const ErrorPage = () => {
  const navigate = useNavigate();
  return (
    <div className="text-center py-24 space-y-4 animate-in fade-in duration-300">
      <span className="material-symbols-outlined text-6xl text-m3-error">error</span>
      <h1 className="text-3xl font-bold">Something went wrong</h1>
      <p className="text-m3-on-surface-variant">An unexpected error occurred. Please try again.</p>
      <Button onClick={() => navigate('/')}>Back home</Button>
    </div>
  );
};
