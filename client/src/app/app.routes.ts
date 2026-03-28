import { Routes } from '@angular/router';
import { Home } from './home/home';
import { ScreenplayViewer } from './screenplay-viewer/screenplay-viewer';

export const routes: Routes = [
  { path: '', component: Home },
  { path: 'screenplays/:id', component: ScreenplayViewer },
];
