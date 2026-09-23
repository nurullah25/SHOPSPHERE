import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { filter, switchMap } from 'rxjs';
import { AdminCatalogService } from '../../services/admin-catalog.service';
import { AdminCategory } from '../../../core/models/admin-catalog.models';
import { CategoryDialog, CategoryDialogData } from '../category-dialog/category-dialog';
import { ConfirmDialogService } from '../../../shared/components/confirm-dialog/confirm-dialog';
import { NotificationService } from '../../../core/services/notification.service';
import { FlatNode, flattenTree } from '../../../shared/utils/category-tree';

@Component({
  selector: 'app-admin-categories',
  imports: [MatButtonModule, MatIconModule, MatTooltipModule],
  templateUrl: './admin-categories.html',
  styleUrl: './admin-categories.scss'
})
export class AdminCategories {
  private readonly catalog = inject(AdminCatalogService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly notifications = inject(NotificationService);

  protected readonly categories = signal<FlatNode<AdminCategory>[]>([]);
  protected readonly loading = signal(true);

  constructor() {
    this.load();
  }

  openDialog(category: AdminCategory | null, defaultParentId: number | null = null): void {
    const data: CategoryDialogData = {
      category,
      defaultParentId,
      parentOptions: this.categories().map(item => ({
        id: item.node.id,
        label: `${'  '.repeat(item.depth)}${item.node.name}`
      }))
    };

    this.dialog
      .open(CategoryDialog, { data, width: '480px' })
      .afterClosed()
      .pipe(filter(Boolean))
      .subscribe(() => {
        this.notifications.success(category ? 'Category saved.' : 'Category created.');
        this.load();
      });
  }

  deleteCategory(category: AdminCategory): void {
    this.confirmDialog
      .confirm({
        title: `Delete ${category.name}?`,
        message: 'Categories that still contain products or subcategories cannot be deleted.',
        confirmText: 'Delete',
        destructive: true
      })
      .pipe(
        filter(Boolean),
        switchMap(() => this.catalog.deleteCategory(category.id))
      )
      .subscribe(() => {
        this.notifications.success(`${category.name} was deleted.`);
        this.load();
      });
  }

  private load(): void {
    this.loading.set(true);
    this.catalog.getCategories().subscribe({
      next: tree => {
        this.categories.set(flattenTree(tree));
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}
