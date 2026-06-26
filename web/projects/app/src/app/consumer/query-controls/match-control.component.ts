import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { translateText, type MatchMode } from 'core';

/**
 * Match-mode control (dumb / presentational).
 *
 * Renders the deterministic Or/And predicate (CLAUDE.md §6, invariant #1 — no NLP): `or` = "at least
 * one selected item present", `and` = a **combo** ("all selected present"). The And option is
 * explicitly labelled as a combo so the user understands it is the "pyure + shnitsel" comb-search,
 * not a stricter OR. It owns no state — the container binds the current {@link MatchMode} and the
 * component emits the chosen mode; pure inputs/outputs so it mirrors 1:1 onto an RN segmented control.
 *
 * Built on Material's button-toggle group, which gives an accessible single-select radio-group
 * (roving focus, arrow-key navigation, `aria-pressed`) for free (WCAG). A short helper line explains
 * the combo semantics so the choice is never ambiguous.
 */
@Component({
  selector: 'app-match-control',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonToggleModule, MatIconModule, TranslatePipe],
  template: `
    <fieldset class="dp-match" data-testid="match-control">
      <legend class="dp-match__legend">{{ 'consumer.match.legend' | translate }}</legend>

      <mat-button-toggle-group
        class="dp-match__group"
        [value]="value()"
        (change)="modeChange.emit($event.value)"
        [attr.aria-label]="'consumer.match.legend' | translate"
      >
        <mat-button-toggle value="or" data-testid="match-or">
          <mat-icon aria-hidden="true">join_inner</mat-icon>
          {{ 'consumer.match.or' | translate }}
        </mat-button-toggle>
        <mat-button-toggle value="and" data-testid="match-and">
          <mat-icon aria-hidden="true">join_full</mat-icon>
          {{ 'consumer.match.and' | translate }}
        </mat-button-toggle>
      </mat-button-toggle-group>

      <p class="dp-match__hint" data-testid="match-hint">{{ hint() }}</p>
    </fieldset>
  `,
  styles: `
    .dp-match {
      display: flex;
      flex-direction: column;
      gap: var(--dp-space-2);
      margin: 0;
      padding: 0;
      border: none;
    }

    .dp-match__legend {
      padding: 0;
      font-size: var(--dp-font-size-subtitle);
      font-weight: var(--dp-font-weight-bold);
    }

    .dp-match__group {
      align-self: flex-start;
    }

    .dp-match__hint {
      margin: 0;
      color: var(--dp-color-on-surface-variant);
      font-size: var(--dp-font-size-caption);
    }
  `,
})
export class MatchControlComponent {
  private readonly translate = inject(TranslateService);

  /** The currently selected match mode. */
  readonly value = input.required<MatchMode>();

  /** Emitted with the newly chosen match mode. */
  readonly modeChange = output<MatchMode>();

  /**
   * Helper line that makes the combo semantics explicit: `and` means **all** selected items must be
   * present in the same venue (a combo), `or` means at least one. The key arrow tracks `value()` and
   * the reactive `translate()` signal re-resolves on a language switch.
   */
  readonly hint = translateText(this.translate, () =>
    this.value() === 'and' ? 'consumer.match.hintAnd' : 'consumer.match.hintOr',
  );
}
