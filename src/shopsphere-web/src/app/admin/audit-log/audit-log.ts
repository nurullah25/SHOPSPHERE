import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { PageEvent, MatPaginatorModule } from '@angular/material/paginator';
import { BehaviorSubject, catchError, finalize, of, switchMap } from 'rxjs';
import { toHttpParams } from '../../core/http/query-params';
import { PagedResult } from '../../core/models/catalog.models';
import { EmptyState } from '../../shared/components/empty-state/empty-state';

interface AuditLogEntry {
  id: number;
  action: string;
  entityName: string;
  entityId: string;
  details: string | null;
  changedBy: string | null;
  createdAt: string;
}

@Component({
  selector: 'app-audit-log',
  imports: [DatePipe, ReactiveFormsModule, MatFormFieldModule, MatPaginatorModule, MatSelectModule, MatTableModule, EmptyState],
  templateUrl: './audit-log.html',
  styleUrl: './audit-log.scss'
})
export class AuditLog {
  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder);

  private readonly query$ = new BehaviorSubject({ action: null as string | null, page: 1, pageSize: 25 });

  protected readonly columns = ['action', 'entity', 'details', 'changedBy', 'when'];
  protected readonly result = signal<PagedResult<AuditLogEntry> | null>(null);
  protected readonly loading = signal(true);

  protected readonly actions = toSignal(
    this.http.get<string[]>('/api/admin/audit-logs/actions').pipe(catchError(() => of([]))),
    { initialValue: [] }
  );

  protected readonly filterForm = this.fb.group({ action: [null as string | null] });

  constructor() {
    this.filterForm.valueChanges.pipe(takeUntilDestroyed()).subscribe(value => {
      this.query$.next({ ...this.query$.value, action: value.action ?? null, page: 1 });
    });

    this.query$
      .pipe(
        switchMap(query => {
          this.loading.set(true);
          return this.http
            .get<PagedResult<AuditLogEntry>>('/api/admin/audit-logs', { params: toHttpParams(query) })
            .pipe(
              catchError(() => of(null)),
              finalize(() => this.loading.set(false))
            );
        }),
        takeUntilDestroyed()
      )
      .subscribe(result => this.result.set(result));
  }

  changePage(event: PageEvent): void {
    this.query$.next({ ...this.query$.value, page: event.pageIndex + 1, pageSize: event.pageSize });
  }

  // Details are stored as JSON, shown here as readable key/value pairs
  protected formatDetails(details: string | null): string {
    if (!details) return '';

    try {
      return Object.entries(JSON.parse(details) as Record<string, unknown>)
        .filter(([, value]) => value !== null && value !== '')
        .map(([key, value]) => `${key}: ${value}`)
        .join(' · ');
    } catch {
      return details;
    }
  }
}
