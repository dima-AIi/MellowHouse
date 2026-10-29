import 'vite/modulepreload-polyfill';
import {attachStorefront} from './bridge';
import * as runtime from '@mellow/core';

window.mhReact=attachStorefront(runtime);
document.documentElement.dataset.uiRuntime='react-typescript';
await import('@mellow/site');
