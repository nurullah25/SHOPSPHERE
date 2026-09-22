import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

export function passwordMatchValidator(passwordKey: string, confirmKey: string): ValidatorFn {
  return (group: AbstractControl): ValidationErrors | null => {
    const password = group.get(passwordKey)?.value;
    const confirm = group.get(confirmKey)?.value;

    return password && confirm && password !== confirm ? { passwordMismatch: true } : null;
  };
}

// Same rule as the API: at least one letter and one number
export const PASSWORD_PATTERN = /^(?=.*[A-Za-z])(?=.*\d).+$/;
