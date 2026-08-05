PRAGMA foreign_keys = ON;

CREATE TABLE products (
  id TEXT PRIMARY KEY NOT NULL,
  sku TEXT NOT NULL,
  name TEXT NOT NULL,
  description TEXT,
  price REAL NOT NULL CHECK (price >= 0),
  category_id TEXT,
  is_active INTEGER NOT NULL DEFAULT 1 CHECK (is_active IN (0, 1)),
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL,
  FOREIGN KEY (category_id) REFERENCES categories (id) ON DELETE SET NULL
);

CREATE UNIQUE INDEX ux_products_sku ON products (sku);
CREATE INDEX ix_products_category_id ON products (category_id);
