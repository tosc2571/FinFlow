import { AfterViewInit, Component, ElementRef, Input, OnChanges, OnDestroy, ViewChild } from '@angular/core';
import { Chart, ChartConfiguration, registerables } from 'chart.js';

Chart.register(...registerables);

// Chart chrome defaults (validated reference palette: muted ink + hairline grid).
Chart.defaults.color = '#898781';
Chart.defaults.borderColor = '#e1e0d9';
Chart.defaults.font.family = 'system-ui, -apple-system, "Segoe UI", sans-serif';

/**
 * Thin wrapper around chart.js. Deliberately not ng2-charts: the app runs
 * zoneless (Angular 21 default) and chart.js is imperative anyway, so a
 * ~40-line wrapper avoids a peer-dependency on a directive library.
 */
@Component({
  selector: 'app-chart',
  template: '<div class="chart-box"><canvas #canvas></canvas></div>',
  styles: ':host { display: block; } .chart-box { position: relative; height: 320px; }',
})
export class ChartComponent implements AfterViewInit, OnChanges, OnDestroy {
  @ViewChild('canvas') canvas?: ElementRef<HTMLCanvasElement>;
  @Input({ required: true }) config!: ChartConfiguration;

  private chart?: Chart;

  ngAfterViewInit(): void {
    this.render();
  }

  ngOnChanges(): void {
    this.render();
  }

  ngOnDestroy(): void {
    this.chart?.destroy();
  }

  private render(): void {
    if (!this.canvas) return;
    this.chart?.destroy();
    this.chart = new Chart(this.canvas.nativeElement, {
      ...this.config,
      options: {
        responsive: true,
        maintainAspectRatio: false,
        ...(this.config.options ?? {}),
      },
    });
  }
}
