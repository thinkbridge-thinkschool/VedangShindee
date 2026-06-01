import { Component } from '@angular/core';
import { QuotesList } from './quotes-list/quotes-list';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [QuotesList],
  template: `<app-quotes-list />`,
})
export class App {}
