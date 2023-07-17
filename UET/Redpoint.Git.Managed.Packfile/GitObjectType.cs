namespace Redpoint.Git.Managed.Packfile
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents the type of packfile entry.
    /// </summary>
    public enum GitObjectType
    {
        /// <summary>
        /// This object is undefined.
        /// </summary>
        Undefined = 0b000,

        /// <summary>
        /// This object is a commit.
        /// </summary>
        Commit = 0b001,

        /// <summary>
        /// This object is a tree.
        /// </summary>
        Tree = 0b010,

        /// <summary>
        /// This object is a blob.
        /// </summary>
        Blob = 0b011,

        /// <summary>
        /// This object is an annotated tag.
        /// </summary>
        Tag = 0b100,

        /// <summary>
        /// A delta representation, where the base object 
        /// is referred to by an offset within the same packfile.
        /// </summary>
        OfsDelta = 0b110,

        /// <summary>
        /// A delta representation, where the base object
        /// is referred to by a SHA-1 hash.
        /// </summary>
        RefDelta = 0b111,
    }
}
