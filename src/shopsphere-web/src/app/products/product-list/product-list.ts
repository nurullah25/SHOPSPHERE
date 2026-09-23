import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { PageEvent, MatPaginatorModule } from '@angular/material/paginator';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { catchError, map, of, switchMap, tap } from 'rxjs';
import { CatalogService } from '../catalog.service';
import { PagedResult, ProductListItem, ProductQuery, ProductSort } from '../../core/models/catalog.models';
import { EmptyState } from '../../shared/components/empty-state/empty-state';
import { ProductCard } from '../../shared/components/product-card/product-card';
import { ProductFilters } from '../product-filters/product-filters';

const SORT_OPTIONS: { value: ProductSort; label: string }[] = [
  { value: 'newest', label: 'Newest' },
  { value: 'price_asc', label: 'Price: low to high' },
  { value: 'price_desc', label: 'Price: high to low' },
  { value: 'name', label: 'Name' },
  { value: 'rating', label: 'Customer rating' }
];

@Component({
  selector: 'app-product-list',
  imports: [
    MatButtonModule,
    MatChipsModule,
    MatFormFieldModule,
    MatIconModule,
    MatPaginatorModule,
    MatSelectModule,
    EmptyState,
    ProductCard,
    ProductFilters
  ],
  templateUrl: './product-list.html',
  styleUrl: './product-list.scss'
})
export class ProductList {
  private readonly catalog = inject(CatalogService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly sortOptions = SORT_OPTIONS;

  protected readonly categories = toSignal(this.catalog.getCategoryTree().pipe(catchError(() => of([]))), {
    initialValue: []
  });

  protected readonly query = signal<ProductQuery>(defaultQuery());
  protected readonly result = signal<PagedResult<ProductListItem> | null>(null);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);
  protected readonly filtersOpen = signal(false);

  protected readonly hasFilters = computed(() => {
    const query = this.query();
    return !!(query.search || query.category || query.minPrice || query.maxPrice || query.inStock);
  });

  protected readonly categoryName = computed(() => {
    const slug = this.query().category;
    if (!slug) return null;

    const find = (nodes = this.categories()): string | null => {
      for (const node of nodes) {
        if (node.slug === slug) return node.name;
        const inChildren = find(node.children);
        if (inChildren) return inChildren;
      }
      return null;
    };
    return find();
  });

  constructor() {
    // Filters live in the URL, so results are shareable and the back button works.
    // switchMap drops the response of a query the user has already moved past.
    this.route.queryParamMap
      .pipe(
        map(params => readQuery(params)),
        tap(query => {
          this.query.set(query);
          this.loading.set(true);
          this.failed.set(false);
        }),
        switchMap(query =>
          this.catalog.searchProducts(query).pipe(
            catchError(() => {
              this.failed.set(true);
              return of(null);
            })
          )
        ),
        takeUntilDestroyed()
      )
      .subscribe(result => {
        this.result.set(result);
        this.loading.set(false);
      });
  }

  applyFilters(changes: Partial<ProductQuery>): void {
    this.updateUrl({ ...changes, page: 1 });
  }

  changeSort(sort: ProductSort): void {
    this.updateUrl({ sort, page: 1 });
  }

  changePage(event: PageEvent): void {
    this.updateUrl({ page: event.pageIndex + 1, pageSize: event.pageSize });
  }

  clearSearch(): void {
    this.updateUrl({ search: undefined, page: 1 });
  }

  private updateUrl(changes: Partial<ProductQuery>): void {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { ...changes, page: changes.page === 1 ? null : changes.page },
      queryParamsHandling: 'merge'
    });
  }
}

function defaultQuery(): ProductQuery {
  return { sort: 'newest', page: 1, pageSize: 12 };
}

function readQuery(params: { get(key: string): string | null }): ProductQuery {
  const number = (key: string) => {
    const value = Number(params.get(key));
    return Number.isFinite(value) && value > 0 ? value : undefined;
  };

  const sort = params.get('sort') as ProductSort | null;

  return {
    search: params.get('search') ?? undefined,
    category: params.get('category') ?? undefined,
    minPrice: number('minPrice'),
    maxPrice: number('maxPrice'),
    inStock: params.get('inStock') === 'true' ? true : undefined,
    sort: SORT_OPTIONS.some(option => option.value === sort) ? sort! : 'newest',
    page: number('page') ?? 1,
    pageSize: number('pageSize') ?? 12
  };
}
