import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { toSignal } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { AdminInventoryService } from '../../services/admin-inventory.service';
import { InventoryItem, InventoryMovement } from '../../../core/models/inventory.models';
import { getErrorMessage } from '../../../core/http/error-message';

@Component({
  selector: 'app-stock-adjustment-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule
  ],
  templateUrl: './stock-adjustment-dialog.html',
  styleUrl: './stock-adjustment-dialog.scss'
})
export class StockAdjustmentDialog {
  private readonly inventory = inject(AdminInventoryService);
  private readonly dialogRef = inject(MatDialogRef<StockAdjustmentDialog, InventoryMovement>);
  private readonly fb = inject(FormBuilder);

  protected readonly item = inject<InventoryItem>(MAT_DIALOG_DATA);
  protected readonly saving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    direction: ['add' as 'add' | 'remove'],
    quantity: [1, [Validators.required, Validators.min(1), Validators.max(100000)]],
    reason: ['Restock' as 'Restock' | 'Adjustment'],
    note: ['']
  });

  private readonly formValue = toSignal(this.form.valueChanges, { initialValue: this.form.getRawValue() });

  protected readonly stockAfter = computed(() => {
    const value = this.formValue();
    const change = (value.direction === 'remove' ? -1 : 1) * (value.quantity ?? 0);
    return this.item.stockQuantity + change;
  });

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();

    this.saving.set(true);
    this.errorMessage.set(null);

    this.inventory
      .adjustStock(this.item.productId, {
        quantityChange: value.direction === 'remove' ? -value.quantity : value.quantity,
        reason: value.reason,
        note: value.note.trim() || null
      })
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: movement => this.dialogRef.close(movement),
        error: error => this.errorMessage.set(getErrorMessage(error))
      });
  }
}
