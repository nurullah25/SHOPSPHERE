import { HttpContext, HttpContextToken } from '@angular/common/http';

// Set on requests where the component shows the error itself (e.g. forms)
export const SKIP_ERROR_TOAST = new HttpContextToken<boolean>(() => false);

// Set on background requests that shouldn't show the global progress bar
export const SKIP_LOADING = new HttpContextToken<boolean>(() => false);

export function silentErrors(): HttpContext {
  return new HttpContext().set(SKIP_ERROR_TOAST, true);
}
