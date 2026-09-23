import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ProductListItem } from '../../../core/models/catalog.models';
import { Price } from '../price/price';
import { ProductPhoto } from '../product-photo/product-photo';
import { RatingStars } from '../rating-stars/rating-stars';

@Component({
  selector: 'app-product-card',
  imports: [RouterLink, Price, ProductPhoto, RatingStars],
  templateUrl: './product-card.html',
  styleUrl: './product-card.scss'
})
export class ProductCard {
  readonly product = input.required<ProductListItem>();
}
