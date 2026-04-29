const express = require('express');
const fs = require('fs');
const fsp = require('fs/promises');
const path = require('path');
const multer = require('multer');

const app = express();
const port = 3000;
const storageDir = path.join(__dirname, 'storage');
const dataFile = path.join(storageDir, 'books.json');
const upload = multer({ storage: multer.memoryStorage() });

app.use(express.json());
app.use('/storage', express.static(storageDir));

let books = [];
let nextId = 1;

function getContentType(filePath) {
   switch (path.extname(filePath).toLowerCase()) {
      case '.epub':
         return 'application/epub+zip';
      case '.pdf':
         return 'application/pdf';
      default:
         return 'application/octet-stream';
   }
}

function toPublicBook(book) {
   return {
      id: book.id,
      title: book.title,
      author: book.author,
      description: book.description ?? '',
      cover_image_path: book.coverImagePath ?? '',
      epub_file_path: book.epubFilePath,
      file_size_bytes: book.fileSizeBytes,
      isbn: book.isbn ?? '',
      language: book.language ?? '',
      publish_date: book.publishDate ?? null,
      uploaded_at: book.uploadedAt,
   };
}

function makeFileName(bookId, originalName) {
   const parsed = path.parse(originalName);
   const safeBaseName = parsed.name.replace(/[^a-z0-9-_]+/gi, '_').replace(/^_+|_+$/g, '') || 'book';
   return `${bookId}-${safeBaseName}${parsed.ext.toLowerCase()}`;
}

function findBook(bookId) {
   return books.find((book) => book.id === bookId);
}

function resolveBookPath(book) {
   return path.join(__dirname, book.epubFilePath);
}

async function ensureStorage() {
   await fsp.mkdir(storageDir, { recursive: true });
}

async function loadBooks() {
   try {
      const rawBooks = await fsp.readFile(dataFile, 'utf8');
      const parsedBooks = JSON.parse(rawBooks);
      return Array.isArray(parsedBooks) ? parsedBooks : [];
   } catch (error) {
      if (error.code === 'ENOENT') {
         return [];
      }

      throw error;
   }
}

function normalizeBookRecord(book) {
   return {
      id: Number.parseInt(book.id, 10),
      title: book.title ?? '',
      author: book.author ?? '',
      description: book.description ?? '',
      coverImagePath: book.coverImagePath ?? book.cover_image_path ?? '',
      epubFilePath: book.epubFilePath ?? book.epub_file_path ?? '',
      fileSizeBytes: Number(book.fileSizeBytes ?? book.file_size_bytes ?? 0),
      isbn: book.isbn ?? '',
      language: book.language ?? '',
      publishDate: book.publishDate ?? book.publish_date ?? null,
      uploadedAt: book.uploadedAt ?? book.uploaded_at ?? new Date().toISOString(),
   };
}

async function saveBooks() {
   await fsp.writeFile(dataFile, JSON.stringify(books, null, 2), 'utf8');
}

async function seedMissingBooks() {
   let addedCount = 0;

   const seeds = [
      {
         title: 'Oliver Twist',
         author: 'Charles Dickens',
         sourcePath: path.join(__dirname, 'Dickens, Charles - Oliver Twist.epub'),
         displayName: 'Oliver Twist.epub',
         description: 'Roman d\'apprentissage de Charles Dickens.',
         language: 'fr',
      },
      {
         title: 'Les trois mousquetaires',
         author: 'Alexandre Dumas',
         sourcePath: path.join(__dirname, 'Dumas, Alexandre - Les trois mousquetaires.epub'),
         displayName: 'Les trois mousquetaires.epub',
         description: 'Roman d\'aventure d\'Alexandre Dumas.',
         language: 'fr',
      },
   ];

   for (const seed of seeds) {
      const alreadyPresent = books.some((book) =>
         book.title.toLowerCase() === seed.title.toLowerCase() &&
         book.author.toLowerCase() === seed.author.toLowerCase());

      if (alreadyPresent) {
         continue;
      }

      if (!fs.existsSync(seed.sourcePath)) {
         continue;
      }

      const fileName = makeFileName(nextId, seed.displayName);
      const destinationPath = path.join(storageDir, fileName);
      await fsp.copyFile(seed.sourcePath, destinationPath);

      const stats = await fsp.stat(destinationPath);
      const uploadedAt = new Date().toISOString();

      books.push({
         id: nextId,
         title: seed.title,
         author: seed.author,
         description: seed.description,
         coverImagePath: '',
         epubFilePath: `storage/${fileName}`,
         fileSizeBytes: stats.size,
         isbn: '',
         language: seed.language,
         publishDate: null,
         uploadedAt,
      });

      nextId += 1;
      addedCount += 1;
   }

   if (addedCount > 0) {
      await saveBooks();
   }

   return addedCount;
}

app.get('/health', (req, res) => {
   res.json({ status: 'ok' });
});

app.get('/books', (req, res) => {
   // Support pagination via ?page=1&pageSize=10
   const page = Math.max(1, Number.parseInt(req.query.page || '1', 10));
   const pageSize = Math.max(1, Number.parseInt(req.query.pageSize || '10', 10));

   const start = (page - 1) * pageSize;
   const items = books.slice(start, start + pageSize).map(toPublicBook);

   return res.json({ items, total: books.length });
});

app.get('/book/:id', (req, res) => {
   const bookId = Number.parseInt(req.params.id, 10);
   const book = findBook(bookId);

   if (!book) {
      return res.status(404).json({ message: 'Book not found' });
   }

   return res.json(toPublicBook(book));
});

app.get('/book/:id/file', (req, res) => {
   const bookId = Number.parseInt(req.params.id, 10);
   const book = findBook(bookId);

   if (!book) {
      return res.status(404).json({ message: 'Book not found' });
   }

   const filePath = resolveBookPath(book);

   if (!fs.existsSync(filePath)) {
      return res.status(404).json({ message: 'Book file not found' });
   }

   return res
      .type(getContentType(filePath))
      .download(filePath, `${book.title}${path.extname(filePath)}`);
});

app.post('/books/upload', upload.single('file'), async (req, res) => {
   try {
      const title = (req.body.title ?? '').trim();
      const author = (req.body.author ?? '').trim();
      const description = (req.body.description ?? '').trim();

      if (!title || !author) {
         return res.status(400).json({ message: 'Title and author are required' });
      }

      if (!req.file) {
         return res.status(400).json({ message: 'Book file is required' });
      }

      const fileName = makeFileName(nextId, req.file.originalname || 'book.epub');
      const destinationPath = path.join(storageDir, fileName);
      await fsp.writeFile(destinationPath, req.file.buffer);

      const stats = await fsp.stat(destinationPath);
      const newBook = {
         id: nextId,
         title,
         author,
         description,
         coverImagePath: (req.body.cover_image_path ?? req.body.coverImagePath ?? '').trim(),
         epubFilePath: `storage/${fileName}`,
         fileSizeBytes: stats.size,
         isbn: (req.body.isbn ?? '').trim(),
         language: (req.body.language ?? '').trim(),
         publishDate: req.body.publish_date ? new Date(req.body.publish_date).toISOString() : null,
         uploadedAt: new Date().toISOString(),
      };

      books.push(newBook);
      nextId += 1;
      await saveBooks();

      return res.status(201).json(toPublicBook(newBook));
   } catch (error) {
      return res.status(500).json({ message: error.message });
   }
});

app.post('/books/restore', async (req, res) => {
   try {
      await seedMissingBooks();
      books = (await loadBooks()).map(normalizeBookRecord);
      nextId = books.reduce((maxId, book) => Math.max(maxId, book.id), 0) + 1;
      return res.json({ message: 'Books restored', count: books.length });
   } catch (error) {
      return res.status(500).json({ message: error.message });
   }
});

app.delete('/book/:id/delete', async (req, res) => {
   try {
      const bookId = Number.parseInt(req.params.id, 10);
      const index = books.findIndex((book) => book.id === bookId);

      if (index === -1) {
         return res.status(404).json({ message: 'Book not found' });
      }

      const [removedBook] = books.splice(index, 1);
      const filePath = resolveBookPath(removedBook);

      if (fs.existsSync(filePath)) {
         await fsp.unlink(filePath);
      }

      await saveBooks();
      return res.sendStatus(204);
   } catch (error) {
      return res.status(500).json({ message: error.message });
   }
});

app.get('/epub/1', (req, res) => {
   const file = path.join(__dirname, 'Dickens, Charles - Oliver Twist.epub');

   if (!fs.existsSync(file)) {
      return res.status(404).send('File not found');
   }

   return res.download(file);
});

app.get('/epub/2', (req, res) => {
   const file = path.join(__dirname, 'Dumas, Alexandre - Les trois mousquetaires.epub');

   if (!fs.existsSync(file)) {
      return res.status(404).send('File not found');
   }

   return res.download(file);
});

async function startServer() {
   await ensureStorage();
   books = (await loadBooks()).map(normalizeBookRecord);
   const addedCount = await seedMissingBooks();

   if (addedCount > 0) {
      books = (await loadBooks()).map(normalizeBookRecord);
   }

   nextId = books.reduce((maxId, book) => Math.max(maxId, book.id), 0) + 1;

   app.listen(port, () => {
      console.log(`Server listening on port ${port}`);
   });
}

startServer().catch((error) => {
   console.error(error);
   process.exit(1);
});