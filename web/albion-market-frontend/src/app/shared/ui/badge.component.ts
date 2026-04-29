import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export type BadgeTone =
  | 'neutral'
  | 'gold'
  | 'blue'
  | 'purple'
  | 'green'
  | 'red'
  | 'amber'
  | 'royal'
  | 'rest'
  | 'caerleon'
  | 'brecilien'
  | 'smugglers-den'
  | 'black-market';

@Component({
  selector: 'ui-badge',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span [class]="classes()" class="inline-flex items-center gap-1 rounded-md border px-1.5 py-0.5 text-[11px] font-semibold leading-none">
      <ng-content />
    </span>
  `,
})
export class BadgeComponent {
  readonly tone = input<BadgeTone>('neutral');
  readonly subtle = input<boolean>(true);

  readonly classes = computed(() => {
    switch (this.tone()) {
      case 'gold':         return 'bg-gold/15 text-gold border-gold/30';
      case 'blue':
      case 'royal':        return 'bg-blue/15 text-blue border-blue/30';
      case 'purple':       return 'bg-purple/15 text-purple border-purple/30';
      case 'green':        return 'bg-profit/15 text-profit border-profit/30';
      case 'red':          return 'bg-danger/15 text-danger border-danger/30';
      case 'amber':        return 'bg-warn/15 text-warn border-warn/30';
      case 'rest':         return 'bg-blue/10 text-blue/90 border-blue/20';
      case 'caerleon':     return 'bg-purple/15 text-purple border-purple/30';
      case 'brecilien':    return 'bg-blue/10 text-blue border-blue/25';
      case 'smugglers-den':return 'bg-warn/15 text-warn border-warn/30';
      case 'black-market': return 'bg-purple/15 text-gold border-gold/40';
      case 'neutral':
      default:             return 'bg-surface-2 text-text-muted border-border';
    }
  });
}
