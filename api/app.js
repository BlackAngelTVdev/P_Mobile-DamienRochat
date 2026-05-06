const express = require('express');
const fs = require('fs');
const fsp = require('fs/promises');
const path = require('path');
const multer = require('multer');
const sqlite3 = require('sqlite3');
const zlib = require('zlib');
const { Readable } = require('stream');
const { pipeline } = require('stream/promises');

const app = express();
const port = 3000;
const storageDir = path.join(__dirname, 'storage');
const dbFile = path.join(storageDir, 'books.db');
const upload = multer({ storage: multer.memoryStorage() });

app.use(express.json());
app.use('/storage', express.static(storageDir));

const sqlite = sqlite3.verbose();
let db;

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
      excerpt: book.excerpt ?? '',
      cover_image_path: book.cover_image_path ?? '',
      epub_file_path: book.epub_file_path,
      file_size_bytes: book.file_size_bytes ?? 0,
      isbn: book.isbn ?? '',
      language: book.language ?? '',
      publish_date: book.publish_date ?? null,
      uploaded_at: book.uploaded_at,
   };
}

function makeFileName(bookId, originalName) {
   const parsed = path.parse(originalName);
   const safeBaseName = parsed.name.replace(/[^a-z0-9-_]+/gi, '_').replace(/^_+|_+$/g, '') || 'book';
   const extension = parsed.ext ? parsed.ext.toLowerCase() : '.epub';
   return `${bookId}-${safeBaseName}${extension}.gz`;
}

function resolveBookPath(book) {
   return path.join(__dirname, book.epub_file_path);
}

async function ensureStorage() {
   await fsp.mkdir(storageDir, { recursive: true });
}

async function ensureDbFile() {
   const handle = await fsp.open(dbFile, 'a');
   await handle.close();
}

async function resetDbFile() {
   try {
      await fsp.unlink(dbFile);
   } catch (error) {
      if (error.code !== 'ENOENT') {
         throw error;
      }
   }
}

function runDb(sql, params = []) {
   return new Promise((resolve, reject) => {
      db.run(sql, params, function (error) {
         if (error) {
            reject(error);
            return;
         }

         resolve(this);
      });
   });
}

function getDb(sql, params = []) {
   return new Promise((resolve, reject) => {
      db.get(sql, params, (error, row) => {
         if (error) {
            reject(error);
            return;
         }

         resolve(row);
      });
   });
}

function allDb(sql, params = []) {
   return new Promise((resolve, reject) => {
      db.all(sql, params, (error, rows) => {
         if (error) {
            reject(error);
            return;
         }

         resolve(rows);
      });
   });
}

async function initDb() {
   await ensureDbFile();
   console.log(`SQLite DB: ${dbFile}`);
   db = new sqlite.Database(dbFile);

   await runDb(`
      CREATE TABLE IF NOT EXISTS books (
         id INTEGER PRIMARY KEY AUTOINCREMENT,
         title TEXT NOT NULL,
         author TEXT NOT NULL,
         description TEXT,
         excerpt TEXT,
         cover_image_path TEXT,
         epub_file_path TEXT NOT NULL,
         file_size_bytes INTEGER,
         isbn TEXT,
         language TEXT,
         publish_date TEXT,
         uploaded_at TEXT
      )
   `);
}


async function getNextId() {
   const row = await getDb('SELECT COALESCE(MAX(id), 0) + 1 AS nextId FROM books');
   return row?.nextId ?? 1;
}

async function compressBufferToFile(buffer, destinationPath) {
   const gzip = zlib.createGzip({ level: zlib.constants.Z_BEST_COMPRESSION });
   await pipeline(Readable.from(buffer), gzip, fs.createWriteStream(destinationPath));
}

async function compressFileToFile(sourcePath, destinationPath) {
   const gzip = zlib.createGzip({ level: zlib.constants.Z_BEST_COMPRESSION });
   await pipeline(fs.createReadStream(sourcePath), gzip, fs.createWriteStream(destinationPath));
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
      const existing = await getDb(
         'SELECT id FROM books WHERE LOWER(title) = LOWER(?) AND LOWER(author) = LOWER(?)',
         [seed.title, seed.author]
      );

      if (existing) {
         continue;
      }

      if (!fs.existsSync(seed.sourcePath)) {
         continue;
      }

      const nextId = await getNextId();
      const fileName = makeFileName(nextId, seed.displayName);
      const destinationPath = path.join(storageDir, fileName);
      await compressFileToFile(seed.sourcePath, destinationPath);

      const stats = await fsp.stat(seed.sourcePath);
      const uploadedAt = new Date().toISOString();

      await runDb(
         `
         INSERT INTO books (
            id,
            title,
            author,
            description,
            excerpt,
            cover_image_path,
            epub_file_path,
            file_size_bytes,
            isbn,
            language,
            publish_date,
            uploaded_at
         ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
         `,
         [
            nextId,
            seed.title,
            seed.author,
            seed.description,
            '',
            '',
            `storage/${fileName}`,
            stats.size,
            '',
            seed.language,
            null,
            uploadedAt,
         ]
      );

      addedCount += 1;
   }

   return addedCount;
}

app.get('/health', (req, res) => {
   res.json({ status: 'ok' });
});

app.get('/books', async (req, res) => {
   // Support pagination via ?page=1&pageSize=10
   const page = Math.max(1, Number.parseInt(req.query.page || '1', 10));
   const pageSize = Math.max(1, Number.parseInt(req.query.pageSize || '10', 10));

   const start = (page - 1) * pageSize;
   const totalRow = await getDb('SELECT COUNT(*) AS count FROM books');
   const rows = await allDb('SELECT * FROM books ORDER BY id LIMIT ? OFFSET ?', [pageSize, start]);
   const items = rows.map(toPublicBook);

   return res.json({ items, total: totalRow?.count ?? 0 });
});

app.get('/book/:id', async (req, res) => {
   const bookId = Number.parseInt(req.params.id, 10);
   const book = await getDb('SELECT * FROM books WHERE id = ?', [bookId]);

   if (!book) {
      return res.status(404).json({ message: 'Book not found' });
   }

   return res.json(toPublicBook(book));
});

app.get('/book/:id/file', async (req, res) => {
   const bookId = Number.parseInt(req.params.id, 10);
   const book = await getDb('SELECT * FROM books WHERE id = ?', [bookId]);

   if (!book) {
      return res.status(404).json({ message: 'Book not found' });
   }

   const filePath = resolveBookPath(book);

   if (!fs.existsSync(filePath)) {
      return res.status(404).json({ message: 'Book file not found' });
   }

   const contentPath = filePath.endsWith('.gz') ? filePath.slice(0, -3) : filePath;
   const downloadName = `${book.title}${path.extname(contentPath)}`;

   if (filePath.endsWith('.gz')) {
      res.type(getContentType(contentPath));
      res.setHeader('Content-Disposition', `attachment; filename="${downloadName}"`);

      const source = fs.createReadStream(filePath);
      const gunzip = zlib.createGunzip();

      source.on('error', (error) => {
         res.status(500).json({ message: error.message });
      });

      gunzip.on('error', (error) => {
         res.status(500).json({ message: error.message });
      });

      return source.pipe(gunzip).pipe(res);
   }

   return res
      .type(getContentType(filePath))
      .download(filePath, downloadName);
});

app.post('/books/upload', upload.single('file'), async (req, res) => {
   try {
      const title = (req.body.title ?? '').trim();
      const author = (req.body.author ?? '').trim();
      const description = (req.body.description ?? '').trim();
      const excerpt = (req.body.excerpt ?? req.body.extrait ?? '').trim();

      if (!title || !author) {
         return res.status(400).json({ message: 'Title and author are required' });
      }

      if (!req.file) {
         return res.status(400).json({ message: 'Book file is required' });
      }

      const nextId = await getNextId();
      const fileName = makeFileName(nextId, req.file.originalname || 'book.epub');
      const destinationPath = path.join(storageDir, fileName);
      await compressBufferToFile(req.file.buffer, destinationPath);

      const newBook = {
         id: nextId,
         title,
         author,
         description,
         excerpt,
         cover_image_path: (req.body.cover_image_path ?? req.body.coverImagePath ?? '').trim(),
         epub_file_path: `storage/${fileName}`,
         file_size_bytes: req.file.buffer.length,
         isbn: (req.body.isbn ?? '').trim(),
         language: (req.body.language ?? '').trim(),
         publish_date: req.body.publish_date ? new Date(req.body.publish_date).toISOString() : null,
         uploaded_at: new Date().toISOString(),
      };

      await runDb(
         `
         INSERT INTO books (
            id,
            title,
            author,
            description,
            excerpt,
            cover_image_path,
            epub_file_path,
            file_size_bytes,
            isbn,
            language,
            publish_date,
            uploaded_at
         ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
         `,
         [
            newBook.id,
            newBook.title,
            newBook.author,
            newBook.description,
            newBook.excerpt,
            newBook.cover_image_path,
            newBook.epub_file_path,
            newBook.file_size_bytes,
            newBook.isbn,
            newBook.language,
            newBook.publish_date,
            newBook.uploaded_at,
         ]
      );

      return res.status(201).json(toPublicBook(newBook));
   } catch (error) {
      return res.status(500).json({ message: error.message });
   }
});

app.post('/books/restore', async (req, res) => {
   try {
      const addedCount = await seedMissingBooks();
      const totalRow = await getDb('SELECT COUNT(*) AS count FROM books');
      return res.json({ message: 'Books restored', count: totalRow?.count ?? 0, added: addedCount });
   } catch (error) {
      return res.status(500).json({ message: error.message });
   }
});

app.delete('/book/:id/delete', async (req, res) => {
   try {
      const bookId = Number.parseInt(req.params.id, 10);
      const removedBook = await getDb('SELECT * FROM books WHERE id = ?', [bookId]);

      if (!removedBook) {
         return res.status(404).json({ message: 'Book not found' });
      }

      const filePath = resolveBookPath(removedBook);

      if (fs.existsSync(filePath)) {
         await fsp.unlink(filePath);
      }

      await runDb('DELETE FROM books WHERE id = ?', [bookId]);
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
   await resetDbFile();
   await initDb();
   await seedMissingBooks();

   app.listen(port, () => {
      console.log(`Server listening on port ${port}`);
   });
}

startServer().catch((error) => {
   console.error(error);
   process.exit(1);
});