import { Component, effect, inject, input, output, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { PageEvent, MatPaginatorModule } from '@angular/material/paginator';
import { filter, finalize, switchMap } from 'rxjs';
import { ReviewService } from '../review.service';
import { ProductReviews, Review } from '../../core/models/review.models';
import { AuthService } from '../../core/auth/auth.service';
import { ConfirmDialogService } from '../../shared/components/confirm-dialog/confirm-dialog';
import { getErrorMessage } from '../../core/http/error-message';
import { NotificationService } from '../../core/services/notification.service';
import { RatingStars } from '../../shared/components/rating-stars/rating-stars';

@Component({
  selector: 'app-product-reviews',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    RatingStars
  ],
  templateUrl: './product-reviews.html',
  styleUrl: './product-reviews.scss'
})
export class ProductReviewsSection {
  private readonly reviews = inject(ReviewService);
  private readonly auth = inject(AuthService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly notifications = inject(NotificationService);
  private readonly fb = inject(FormBuilder);

  readonly productId = input.required<number>();

  // Lets the product page refresh the rating shown next to the title
  readonly reviewsChanged = output<void>();

  protected readonly ratings = [5, 4, 3, 2, 1];
  protected readonly data = signal<ProductReviews | null>(null);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly editing = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly isSignedIn = signal(false);

  private page = 1;

  protected readonly form = this.fb.nonNullable.group({
    rating: [5, [Validators.required, Validators.min(1), Validators.max(5)]],
    title: ['', Validators.maxLength(150)],
    comment: ['', Validators.maxLength(2000)]
  });

  constructor() {
    effect(() => {
      const productId = this.productId();
      this.page = 1;
      this.editing.set(false);
      this.isSignedIn.set(this.auth.currentUser !== null);
      this.load(productId);
    });
  }

  changePage(event: PageEvent): void {
    this.page = event.pageIndex + 1;
    this.load(this.productId(), event.pageSize);
  }

  startEditing(review: Review): void {
    this.form.setValue({
      rating: review.rating,
      title: review.title ?? '',
      comment: review.comment ?? ''
    });
    this.editing.set(true);
  }

  cancelEditing(): void {
    this.editing.set(false);
    this.errorMessage.set(null);
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const request = {
      rating: value.rating,
      title: value.title.trim() || null,
      comment: value.comment.trim() || null
    };

    const existing = this.data()?.myReview;
    const save$ = existing && this.editing()
      ? this.reviews.update(existing.id, request)
      : this.reviews.create(this.productId(), request);

    this.saving.set(true);
    this.errorMessage.set(null);

    save$.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => {
        this.notifications.success(existing ? 'Your review was updated.' : 'Thanks for your review!');
        this.form.reset({ rating: 5, title: '', comment: '' });
        this.editing.set(false);
        this.page = 1;
        this.load(this.productId());
        this.reviewsChanged.emit();
      },
      error: error => this.errorMessage.set(getErrorMessage(error))
    });
  }

  deleteReview(review: Review): void {
    this.confirmDialog
      .confirm({
        title: 'Delete your review?',
        message: 'Your rating and comment will be removed from this product.',
        confirmText: 'Delete',
        destructive: true
      })
      .pipe(
        filter(Boolean),
        switchMap(() => this.reviews.delete(review.id))
      )
      .subscribe(() => {
        this.notifications.success('Your review was deleted.');
        this.load(this.productId());
        this.reviewsChanged.emit();
      });
  }

  protected barWidth(rating: number): string {
    const data = this.data();
    if (!data || data.reviewCount === 0) return '0%';
    return `${Math.round(((data.ratingCounts[rating] ?? 0) / data.reviewCount) * 100)}%`;
  }

  private load(productId: number, pageSize = 5): void {
    this.loading.set(true);
    this.reviews
      .getReviews(productId, this.page, pageSize)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: data => this.data.set(data),
        error: () => this.data.set(null)
      });
  }
}
