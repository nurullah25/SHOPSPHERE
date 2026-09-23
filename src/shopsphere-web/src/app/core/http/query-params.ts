import { HttpParams } from '@angular/common/http';

// Builds HttpParams from an object, leaving out empty values so the API
// falls back to its own defaults.
export function toHttpParams(values: object): HttpParams {
  let params = new HttpParams();

  for (const [key, value] of Object.entries(values)) {
    if (value !== null && value !== undefined && value !== '') {
      params = params.set(key, String(value));
    }
  }

  return params;
}
