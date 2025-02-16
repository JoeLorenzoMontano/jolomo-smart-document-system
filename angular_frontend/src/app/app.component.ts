import { Component } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { DocumentService } from '../../services/document.service';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css']
})
export class AppComponent {
  selectedFile: File | null = null;
  searchQuery: string = '';
  searchResults: any[] = [];
  searchResultRag?: SafeHtml;

  constructor(private documentService: DocumentService, private sanitizer: DomSanitizer) { }

  onFileSelected(event: any) {
    if (event.target.files.length > 0) {
      this.selectedFile = event.target.files[0];
    }
  }

  uploadFile() {
    if (!this.selectedFile) return;

    this.documentService.uploadFile(this.selectedFile).subscribe({
      next: (response: any) => {
        console.log('File uploaded successfully:', response);
        alert('File uploaded successfully!');
      },
      error: (error: any) => {
        console.error('Upload error:', error);
        alert('File upload failed!');
      }
    });
  }

  searchDocuments() {
    if (!this.searchQuery) return;

    this.documentService.searchDocuments(this.searchQuery).subscribe(result => {
      this.searchResults = result;
    });
  }

  searchRag() {
    if (!this.searchQuery) return;

    this.documentService.searchWithRAG(this.searchQuery).subscribe(result => {
      this.searchResultRag = this.sanitizer.bypassSecurityTrustHtml(result);
    });
  }
}
