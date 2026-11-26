using System.Collections.Generic;
using UnityEngine;

namespace ARCHIV3D
{
    /// <summary>One page of a bookshelf book. Fill these in from the Inspector.</summary>
    [System.Serializable]
    public class BookPage
    {
        [Tooltip("Optional. Shown in bold at the top of the page.")]
        public string heading = "Page heading";

        [TextArea(6, 30)]
        [Tooltip("The page's text. Write as much as you like: whatever doesn't fit continues on the next page(s) " +
                 "by itself. You can use <b>bold</b>, <i>italic</i> and <color=#8B4513>color</color>.")]
        public string text = "Write your idea here.";

        [Tooltip("Optional picture shown above the text (on the first page, if the text runs over several).")]
        public Sprite picture;
    }

    /// <summary>
    /// The shared book logic every bookshelf uses. Each shelf has its own component
    /// (Year1Book, Year2Book, Year3Book, ...) that inherits from this, so every shelf
    /// keeps its own pages but they all work the same way. Fill the pages in on the shelf's Inspector.
    /// Year bookshelves now keep their books in Subjects instead (see YearBookshelf / SubjectData);
    /// Tools > ARCHIV3D > Set Up Year Subject Shelves moves these books into a subject.
    /// </summary>
    public abstract class ShelfBook : MonoBehaviour
    {
        [Tooltip("Big title on the book's first page.")]
        public string title = "Book";

        [Tooltip("Smaller line under the title.")]
        public string subtitle = "ARCHIV3D Library";

        [TextArea(2, 6)]
        [Tooltip("Short text on the first page, under the title.")]
        public string description = "";

        [Tooltip("Color of this book's button when a shelf has more than one book.")]
        public Color coverColor = new Color32(104, 46, 30, 255);

        public List<BookPage> pages = new List<BookPage>();

        public string DisplayTitle => string.IsNullOrWhiteSpace(title) ? GetType().Name : title;

        LearningMaterial _material;

        /// <summary>This book as a learning material, so the book screen can show it next to subject books.</summary>
        public LearningMaterial ToMaterial()
        {
            if (_material == null) _material = new LearningMaterial();
            _material.title = DisplayTitle;
            _material.subtitle = subtitle;
            _material.description = description;
            _material.coverColor = coverColor;
            _material.pages = pages;
            _material.materialId = "shelfbook/" + gameObject.scene.name + "/" + gameObject.name + "/" + GetType().Name;
            return _material;
        }

        protected virtual void Reset()
        {
            title = gameObject.name;
            pages = new List<BookPage> { new BookPage() };
        }
    }
}
