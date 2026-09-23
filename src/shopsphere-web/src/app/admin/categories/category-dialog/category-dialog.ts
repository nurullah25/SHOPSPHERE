import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { finalize } from 'rxjs';
import { AdminCatalogService } from '../../services/admin-catalog.service';
import { AdminCategory } from '../../../core/models/admin-catalog.models';
import { getErrorMessage } from '../../../core/http/error-message';

export interface CategoryDialogData {
  category: AdminCategory | null;
  parentOptions: { id: number; label: string }[];
  defaultParentId?: number | null;
}

@Component({
  selector: 'app-category-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSlideToggleModule
  ],
  templateUrl: './category-dialog.html',
  styleUrl: './category-dialog.scss'
})
export class CategoryDialog {
  private readonly catalog = inject(AdminCatalogService);
  private readonly dialogRef = inject(MatDialogRef<CategoryDialog, AdminCategory>);
  private readonly fb = inject(FormBuilder);

  protected readonly data = inject<CategoryDialogData>(MAT_DIALOG_DATA);
  protected readonly saving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    name: [this.data.category?.name ?? '', [Validators.required, Validators.maxLength(100)]],
    slug: [this.data.category?.slug ?? ''],
    description: [this.data.category?.description ?? ''],
    parentId: [this.data.category?.parentId ?? this.data.defaultParentId ?? null],
    sortOrder: [this.data.category?.sortOrder ?? 0, [Validators.required, Validators.min(0)]],
    isActive: [this.data.category?.isActive ?? true]
  });

  // A category can't become its own parent
  protected readonly parentOptions = this.data.parentOptions.filter(option => option.id !== this.data.category?.id);

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const request = {
      name: value.name,
      slug: value.slug.trim() || null,
      description: value.description.trim() || null,
      parentId: value.parentId,
      sortOrder: value.sortOrder,
      isActive: value.isActive
    };

    this.saving.set(true);
    this.errorMessage.set(null);

    const existing = this.data.category;
    const save$ = existing
      ? this.catalog.updateCategory(existing.id, request)
      : this.catalog.createCategory(request);

    save$.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: category => this.dialogRef.close(category),
      error: error => this.errorMessage.set(getErrorMessage(error))
    });
  }
}
