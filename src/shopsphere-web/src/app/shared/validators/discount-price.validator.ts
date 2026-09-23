import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

// Mirrors the API rule: a discount price only makes sense below the regular price
export function discountBelowPriceValidator(priceKey: string, discountKey: string): ValidatorFn {
  return (group: AbstractControl): ValidationErrors | null => {
    const price = group.get(priceKey)?.value;
    const discount = group.get(discountKey)?.value;

    if (price == null || discount == null || discount === '') {
      return null;
    }

    return Number(discount) >= Number(price) ? { discountTooHigh: true } : null;
  };
}
