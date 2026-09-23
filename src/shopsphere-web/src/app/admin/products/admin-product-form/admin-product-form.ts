import { Component, effect, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTooltipModule } from '@angular/material/tooltip';
import { catchError, filter, finalize, of, switchMap } from 'rxjs';
import { AdminCatalogService } from '../../services/admin-catalog.service';
import { AdminProduct } from '../../../core/models/admin-catalog.models';
import { ProductImage } from '../../../core/models/catalog.models';
import { HasUnsavedChanges } from '../../../core/guards/unsaved-changes.guard';
import { NotificationService } from '../../../core/services/notification.service';
import { ConfirmDialogService } from '../../../shared/components/confirm-dialog/confirm-dialog';
import { getErrorMessage } from '../../../core/http/error-message';
import { flattenTree } from '../../../shared/utils/category-tree';
import { discountBelowPriceValidator } from '../../../shared/validators/discount-price.validator';

const MAX_IMAGE_BYTES = 2 * 1024 * 1024;

@Component({
  selector: 'app-admin-product-form',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatTooltipModule
  ],
  templateUrl: './admin-product-form.html',
  styleUrl: './admin-product-form.scss'
})
export class AdminProductForm implements HasUnsavedChanges {
  private readonly catalog = inject(AdminCatalogService);
  private readonly notifications = inject(NotificationService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);

  // Bound from the :id route parameter, missing when creating a product
  readonly id = input<string>();

  protected readonly saving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly product = signal<AdminProduct | null>(null);
  protected readonly images = signal<ProductImage[]>([]);
  protected readonly uploading = signal(false);

  protected readonly categories = toSignal(this.catalog.getCategories().pipe(catchError(() => of([]))), {
    initialValue: []
  });

  protected readonly form = this.fb.nonNullable.group(
    {
      name: ['', [Validators.required, Validators.maxLength(200)]],
      sku: ['', [Validators.required, Validators.maxLength(50), Validators.pattern(/^[A-Za-z0-9-]+$/)]],
      slug: [''],
      categoryId: [null as number | null, Validators.required],
      price: [0, [Validators.required, Validators.min(0.01)]],
      discountPrice: [null as number | null],
      stockQuantity: [0, [Validators.required, Validators.min(0)]],
      lowStockThreshold: [5, [Validators.required, Validators.min(0)]],
      description: ['', [Validators.required, Validators.maxLength(4000)]],
      isActive: [true]
    },
    { validators: discountBelowPriceValidator('price', 'discountPrice') }
  );

  constructor() {
    effect(() => {
      const id = this.id();
      if (id) {
        this.loadProduct(Number(id));
      } else {
        this.form.controls.stockQuantity.enable();
      }
    });
  }

  protected readonly categoryOptions = () =>
    flattenTree(this.categories()).map(item => ({
      id: item.node.id,
      label: `${'  '.repeat(item.depth)}${item.node.name}`
    }));

  hasUnsavedChanges(): boolean {
    return this.form.dirty && !this.saving();
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const request = {
      name: value.name,
      sku: value.sku,
      slug: value.slug?.trim() || null,
      description: value.description,
      price: value.price,
      discountPrice: value.discountPrice,
      categoryId: value.categoryId!,
      lowStockThreshold: value.lowStockThreshold,
      isActive: value.isActive
    };

    this.saving.set(true);
    this.errorMessage.set(null);

    const existing = this.product();
    const save$ = existing
      ? this.catalog.updateProduct(existing.id, { ...request, rowVersion: existing.rowVersion })
      : this.catalog.createProduct({ ...request, stockQuantity: value.stockQuantity });

    save$.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: product => {
        this.form.markAsPristine();
        this.notifications.success(existing ? 'Product saved.' : 'Product created.');

        if (existing) {
          this.applyProduct(product);
        } else {
          // Go to the edit page so images can be added
          this.router.navigate(['/admin/products', product.id]);
        }
      },
      error: error => this.errorMessage.set(getErrorMessage(error))
    });
  }

  uploadImage(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    const product = this.product();
    if (!file || !product) return;

    input.value = '';

    if (file.size > MAX_IMAGE_BYTES) {
      this.notifications.error('Images must be 2 MB or smaller.');
      return;
    }

    this.uploading.set(true);
    this.catalog
      .uploadImage(product.id, file)
      .pipe(finalize(() => this.uploading.set(false)))
      .subscribe(image => this.images.update(images => [...images, image]));
  }

  setMainImage(image: ProductImage): void {
    const product = this.product();
    if (!product || image.isMain) return;

    this.catalog.setMainImage(product.id, image.id).subscribe(() => {
      this.images.update(images => images.map(i => ({ ...i, isMain: i.id === image.id })));
    });
  }

  deleteImage(image: ProductImage): void {
    const product = this.product();
    if (!product) return;

    this.confirmDialog
      .confirm({ title: 'Delete image?', message: 'The image file will be removed.', confirmText: 'Delete', destructive: true })
      .pipe(filter(Boolean), switchMap(() => this.catalog.deleteImage(product.id, image.id)))
      .subscribe(() => this.images.update(images => images.filter(i => i.id !== image.id)));
  }

  private loadProduct(id: number): void {
    this.catalog.getProduct(id).subscribe({
      next: product => this.applyProduct(product),
      error: () => this.router.navigate(['/admin/products'])
    });
  }

  private applyProduct(product: AdminProduct): void {
    this.product.set(product);
    this.images.set(product.images);

    this.form.patchValue({
      name: product.name,
      sku: product.sku,
      slug: product.slug,
      categoryId: product.categoryId,
      price: product.price,
      discountPrice: product.discountPrice,
      stockQuantity: product.stockQuantity,
      lowStockThreshold: product.lowStockThreshold,
      description: product.description,
      isActive: product.isActive
    });

    // Stock is changed through inventory adjustments, not this form
    this.form.controls.stockQuantity.disable();
    this.form.markAsPristine();
  }
}
