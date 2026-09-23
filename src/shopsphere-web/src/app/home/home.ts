import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { catchError, map, of } from 'rxjs';
import { CatalogService } from '../products/catalog.service';
import { ProductCard } from '../shared/components/product-card/product-card';

@Component({
  selector: 'app-home',
  imports: [RouterLink, MatButtonModule, MatIconModule, ProductCard],
  templateUrl: './home.html',
  styleUrl: './home.scss'
})
export class Home {
  private readonly catalog = inject(CatalogService);

  protected readonly categories = toSignal(
    this.catalog.getCategoryTree().pipe(catchError(() => of([]))),
    { initialValue: [] }
  );

  protected readonly newArrivals = toSignal(
    this.catalog
      .searchProducts({ sort: 'newest', page: 1, pageSize: 8, inStock: true })
      .pipe(
        map(result => result.items),
        catchError(() => of([]))
      ),
    { initialValue: [] }
  );
}
