/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

namespace React.Exceptions
{
    /// <summary>
    /// Thrown when a non-existent component is rendered.
    /// </summary>
    public class ReactInvalidComponentException : ReactException
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ReactInvalidComponentException"/> class.
		/// </summary>
		/// <param name="message">The message that describes the error.</param>
		public ReactInvalidComponentException(string message) : base(message) { }
		/// <summary>
		/// Initializes a new instance of the <see cref="ReactInvalidComponentException"/> class.
		/// </summary>
		/// <param name="message">The error message that explains the reason for the exception.</param>
		/// <param name="innerException">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param>
		public ReactInvalidComponentException(string message, Exception innerException)
			: base(message, innerException) { }
	}
}
