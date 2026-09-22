import { HttpErrorResponse } from '@angular/common/http';

interface ProblemDetails {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

export function getErrorMessage(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) {
    return 'Something went wrong. Please try again.';
  }

  if (error.status === 0) {
    return 'Unable to reach the server. Please check your connection.';
  }

  const problem = error.error as ProblemDetails | null;

  if (problem?.errors) {
    const firstError = Object.values(problem.errors).flat()[0];
    if (firstError) {
      return firstError;
    }
  }

  return problem?.detail ?? problem?.title ?? 'Something went wrong. Please try again.';
}
