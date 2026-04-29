import { Overlay, OverlayRef, ConnectedPosition } from '@angular/cdk/overlay';
import { TemplatePortal } from '@angular/cdk/portal';
import {
  Directive,
  ElementRef,
  EventEmitter,
  HostListener,
  inject,
  Input,
  OnDestroy,
  Output,
  TemplateRef,
  ViewContainerRef,
} from '@angular/core';
import { Subject, takeUntil } from 'rxjs';

@Directive({
  selector: '[uiPopoverTrigger]',
  standalone: true,
  exportAs: 'uiPopover',
})
export class PopoverDirective implements OnDestroy {
  @Input('uiPopoverTrigger') template!: TemplateRef<unknown>;
  @Input() popoverWidth: 'trigger' | number | null = 'trigger';
  @Output() readonly popoverOpened = new EventEmitter<void>();
  @Output() readonly popoverClosed = new EventEmitter<void>();

  private readonly overlay = inject(Overlay);
  private readonly origin = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly viewContainerRef = inject(ViewContainerRef);
  private readonly destroy$ = new Subject<void>();
  private overlayRef: OverlayRef | null = null;

  get isOpen(): boolean {
    return this.overlayRef?.hasAttached() ?? false;
  }

  @HostListener('click')
  toggle(): void {
    if (this.isOpen) {
      this.close();
    } else {
      this.open();
    }
  }

  open(): void {
    if (this.isOpen || !this.template) {
      return;
    }

    const positionStrategy = this.buildPositionStrategy();
    const width = this.popoverWidth === 'trigger'
      ? this.origin.nativeElement.getBoundingClientRect().width
      : this.popoverWidth ?? undefined;

    this.overlayRef = this.overlay.create({
      positionStrategy,
      scrollStrategy: this.overlay.scrollStrategies.reposition(),
      hasBackdrop: true,
      backdropClass: 'cdk-overlay-transparent-backdrop',
      width: width ?? undefined,
      panelClass: 'ui-popover-panel',
    });

    const portal = new TemplatePortal(this.template, this.viewContainerRef);
    this.overlayRef.attach(portal);

    this.overlayRef
      .backdropClick()
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.close());

    this.overlayRef
      .keydownEvents()
      .pipe(takeUntil(this.destroy$))
      .subscribe((event) => {
        if (event.key === 'Escape') {
          event.preventDefault();
          this.close();
        }
      });

    this.popoverOpened.emit();
  }

  close(): void {
    if (this.overlayRef) {
      this.overlayRef.detach();
      this.overlayRef.dispose();
      this.overlayRef = null;
      this.popoverClosed.emit();
    }
  }

  ngOnDestroy(): void {
    this.close();
    this.destroy$.next();
    this.destroy$.complete();
  }

  private buildPositionStrategy() {
    const positions: ConnectedPosition[] = [
      { originX: 'start', originY: 'bottom', overlayX: 'start', overlayY: 'top', offsetY: 6 },
      { originX: 'start', originY: 'top',    overlayX: 'start', overlayY: 'bottom', offsetY: -6 },
    ];

    return this.overlay
      .position()
      .flexibleConnectedTo(this.origin)
      .withPositions(positions)
      .withFlexibleDimensions(false)
      .withPush(true);
  }
}
