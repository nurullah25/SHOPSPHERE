import { inject } from '@angular/core';
import { CanDeactivateFn } from '@angular/router';
import { ConfirmDialogService } from '../../shared/components/confirm-dialog/confirm-dialog';

export interface HasUnsavedChanges {
  hasUnsavedChanges(): boolean;
}

export const unsavedChangesGuard: CanDeactivateFn<HasUnsavedChanges> = component => {
  if (!component.hasUnsavedChanges()) {
    return true;
  }

  return inject(ConfirmDialogService).confirm({
    title: 'Discard changes?',
    message: 'You have unsaved changes on this page. Leave without saving?',
    confirmText: 'Discard',
    destructive: true
  });
};
