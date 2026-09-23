import { Component, effect, inject, input, output } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { debounceTime } from 'rxjs';
import { CategoryNode, ProductQuery } from '../../core/models/catalog.models';
import { FlatNode, flattenTree } from '../../shared/utils/category-tree';

@Component({
  selector: 'app-product-filters',
  imports: [ReactiveFormsModule, MatButtonModule, MatCheckboxModule, MatFormFieldModule, MatInputModule],
  templateUrl: './product-filters.html',
  styleUrl: './product-filters.scss'
})
export class ProductFilters {
  readonly categories = input.required<CategoryNode[]>();
  readonly query = input.required<ProductQuery>();

  readonly filterChange = output<Partial<ProductQuery>>();

  private readonly fb = inject(FormBuilder);

  protected readonly priceForm = this.fb.group({
    minPrice: this.fb.control<number | null>(null),
    maxPrice: this.fb.control<number | null>(null)
  });

  protected flatCategories: FlatNode<CategoryNode>[] = [];

  constructor() {
    effect(() => {
      this.flatCategories = flattenTree(this.categories());

      const { minPrice, maxPrice } = this.query();
      this.priceForm.patchValue({ minPrice: minPrice ?? null, maxPrice: maxPrice ?? null }, { emitEvent: false });
    });

    // Wait for typing to settle before reloading the list
    this.priceForm.valueChanges
      .pipe(debounceTime(600), takeUntilDestroyed())
      .subscribe(value => {
        if (this.priceForm.valid) {
          this.filterChange.emit({ minPrice: value.minPrice ?? undefined, maxPrice: value.maxPrice ?? undefined });
        }
      });
  }

  selectCategory(slug: string | undefined): void {
    this.filterChange.emit({ category: slug });
  }

  toggleInStock(checked: boolean): void {
    this.filterChange.emit({ inStock: checked ? true : undefined });
  }

  clearAll(): void {
    this.priceForm.reset({}, { emitEvent: false });
    this.filterChange.emit({ category: undefined, minPrice: undefined, maxPrice: undefined, inStock: undefined, search: undefined });
  }
}
