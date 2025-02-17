import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AppComponent } from './app.component';
import { DocumentService } from '../../services/document.service';
import { DomSanitizer } from '@angular/platform-browser';
import { of, throwError } from 'rxjs';

describe('AppComponent', () => {
  let component: AppComponent;
  let fixture: ComponentFixture<AppComponent>;
  let documentServiceMock: jasmine.SpyObj<DocumentService>;
  let sanitizerMock: jasmine.SpyObj<DomSanitizer>;

  beforeEach(async () => {
    documentServiceMock = jasmine.createSpyObj('DocumentService', ['uploadFile', 'searchDocuments', 'searchWithRAG']);
    sanitizerMock = jasmine.createSpyObj('DomSanitizer', ['bypassSecurityTrustHtml']);

    await TestBed.configureTestingModule({
      declarations: [AppComponent],
      providers: [
        { provide: DocumentService, useValue: documentServiceMock },
        { provide: DomSanitizer, useValue: sanitizerMock }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AppComponent);
    component = fixture.componentInstance;
  });

  it('should create the component', () => {
    expect(component).toBeTruthy();
  });

  // Test: File selection
  it('should set selectedFile when a file is selected', () => {
    const mockFile = new File(['test content'], 'test.txt', { type: 'text/plain' });
    const event = { target: { files: [mockFile] } };

    component.onFileSelected(event);

    expect(component.selectedFile).toEqual(mockFile);
  });

  // Test: File upload success
  it('should call uploadFile and show success alert on success', () => {
    spyOn(window, 'alert');
    documentServiceMock.uploadFile.and.returnValue(of({ success: true }));

    component.selectedFile = new File(['test content'], 'test.txt', { type: 'text/plain' });
    component.uploadFile();

    expect(documentServiceMock.uploadFile).toHaveBeenCalledWith(component.selectedFile);
    expect(window.alert).toHaveBeenCalledWith('File uploaded successfully!');
  });

  // Test: File upload failure
  it('should show error alert on upload failure', () => {
    spyOn(window, 'alert');
    documentServiceMock.uploadFile.and.returnValue(throwError(() => new Error('Upload error')));

    component.selectedFile = new File(['test content'], 'test.txt', { type: 'text/plain' });
    component.uploadFile();

    expect(documentServiceMock.uploadFile).toHaveBeenCalled();
    expect(window.alert).toHaveBeenCalledWith('File upload failed!');
  });

  // Test: Search documents
  it('should call searchDocuments and store results', () => {
    const mockResults = [{ title: 'Test Document' }];
    documentServiceMock.searchDocuments.and.returnValue(of(mockResults));

    component.searchQuery = 'Test Query';
    component.searchDocuments();

    expect(documentServiceMock.searchDocuments).toHaveBeenCalledWith('Test Query');
    expect(component.searchResults).toEqual(mockResults);
  });

  // Test: RAG search and text formatting
  it('should call searchWithRAG', () => {
    const mockResponse = '### Header\n\n- Item 1\n- Item 2';

    documentServiceMock.searchWithRAG.and.returnValue(of(mockResponse));

    component.searchQuery = 'Test Query';
    component.searchRag();

    expect(documentServiceMock.searchWithRAG).toHaveBeenCalledWith('Test Query');
  });
});
