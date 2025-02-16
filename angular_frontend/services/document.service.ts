import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class DocumentService {
  private apiUrl = 'https://localhost:44360/api/upload'; // Adjust API Gateway URL if needed

  constructor(private http: HttpClient) {}

  uploadFile(file: File): Observable<any> {
    const formData = new FormData();
    formData.append('file', file);

    return this.http.post(`${this.apiUrl}`, formData);
  }

  /** ✅ Searches for documents based on the query text */
  searchDocuments(query: string, topResults: number = 5, includeOriginalText: boolean = true): Observable<any> {
    let params = new HttpParams()
      .set('query', query);
      //.set('topResults', topResults.toString())
      //.set('includeOriginalText', includeOriginalText.toString());

    return this.http.get(`${this.apiUrl}/search`, { params });
  }
}
