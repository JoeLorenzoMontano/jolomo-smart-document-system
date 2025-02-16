import { NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { HttpClientModule } from '@angular/common/http';
import { AppComponent } from './app.component';
import { DocumentService } from '../../services/document.service';
import { FormsModule } from '@angular/forms';

@NgModule({
  declarations: [
    AppComponent
  ],
  imports: [
    BrowserModule, FormsModule,
    HttpClientModule 
  ],
  providers: [DocumentService],
  bootstrap: [AppComponent]
})
export class AppModule { }
