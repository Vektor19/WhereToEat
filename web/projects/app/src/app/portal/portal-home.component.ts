import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

/**
 * Operator-portal landing / dashboard placeholder (Step 18).
 *
 * The guarded entry point the {@link adminGuard} protects: it renders inside the
 * {@link PortalShellComponent} content outlet and gives the admin/operator a clear "you are in the
 * portal" landing. It is intentionally a scaffold — the real management features (menu / protection /
 * address / photos / verified / ads / analytics) are added by Steps 19/20/24 as sibling routes the
 * side-nav already links to; this step delivers only the shell + nav + the protected landing area.
 *
 * Copy resolves from the runtime i18n catalogs (Step 17); themed from design tokens (no raw literals).
 */
@Component({
  selector: 'app-portal-home',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe],
  template: `
    <section class="dp-portal-home" data-testid="portal-home">
      <h1 class="dp-portal-home__title">{{ 'portal.home.title' | translate }}</h1>
      <p class="dp-portal-home__intro">{{ 'portal.home.intro' | translate }}</p>
    </section>
  `,
  styles: `
    :host {
      display: block;
    }

    .dp-portal-home {
      display: flex;
      flex-direction: column;
      gap: var(--dp-space-3);
      max-width: var(--dp-layout-content-max);
    }

    .dp-portal-home__title {
      margin: 0;
      font-size: var(--dp-font-size-title);
      font-weight: var(--dp-font-weight-bold);
    }

    .dp-portal-home__intro {
      margin: 0;
      font-size: var(--dp-font-size-body);
      color: var(--dp-color-on-surface-variant);
    }
  `,
})
export class PortalHomeComponent {}
