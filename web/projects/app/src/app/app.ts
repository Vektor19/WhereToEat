import { ChangeDetectionStrategy, Component } from '@angular/core';
import { AppShellComponent } from './shell/app-shell.component';

/**
 * Root component — renders the responsive {@link AppShellComponent}, which owns
 * the top bar (with the language-switch slot), the routed content outlet, and the
 * global footer hosting the single data-accuracy notice.
 */
@Component({
  selector: 'app-root',
  imports: [AppShellComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {}
