import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TranslateService } from '@ngx-translate/core';

/**
 * Shared loading indicator (dumb / presentational).
 *
 * The single spinner the features reuse so loading looks the same everywhere (DRY). It is driven by
 * an input rather than owning any state, so a smart container points it at the `loading` signal of
 * its `RequestState` (the shared async-state primitive in `core`). Themed entirely from design
 * tokens via Material; no raw literals. The label is a resolved string the container passes (usually
 * from a catalog key); when omitted it falls back to the generic `common.loading` catalog copy
 * (Step 17) — so the component holds no launch-locale string.
 */
@Component({
  selector: 'app-loading',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatProgressSpinnerModule],
  template: `
    <div class="dp-loading" role="status" aria-live="polite" data-testid="loading">
      <mat-progress-spinner mode="indeterminate" diameter="40" />
      <span class="dp-loading__label">{{ resolvedLabel() }}</span>
    </div>
  `,
  styles: `
    .dp-loading {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--dp-space-3);
      padding: var(--dp-space-6) var(--dp-space-4);
      color: var(--dp-color-on-surface-variant);
    }

    .dp-loading__label {
      font-size: var(--dp-font-size-caption);
    }
  `,
})
export class LoadingComponent {
  private readonly translate = inject(TranslateService);

  /** Accessible / visible label shown beneath the spinner (a resolved string, usually a catalog key). */
  readonly label = input<string | undefined>(undefined);

  /** The generic loading copy used when no `label` was supplied. */
  private readonly defaultLabel = this.translate.translate('common.loading');

  /** The label to show: the supplied one, else the generic catalog copy. */
  readonly resolvedLabel = computed<string>(() => this.label() ?? (this.defaultLabel() as string));
}
