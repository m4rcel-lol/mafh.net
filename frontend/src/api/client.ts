import { mockClient } from './mockClient';
import { backendClient } from './backendClient';

const useMockApi = import.meta.env.VITE_USE_MOCK_API === 'true' || import.meta.env.VITE_USE_MOCK_API === undefined;

export const api = useMockApi ? mockClient : backendClient;

export const isMockMode = useMockApi;
