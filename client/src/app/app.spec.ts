import { TestBed } from '@angular/core/testing';
import { App } from './app';
import { GreetService } from '../lib/services/greet.service';
import { ParserService } from '../lib/services/parser.service';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        { provide: GreetService, useValue: { sayHello: async () => 'Hello Test' } },
        { provide: ParserService, useValue: { uploadPdf: async () => ({ ok: true }) } },
      ],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should render title', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Screenplay Viewer');
  });
});
