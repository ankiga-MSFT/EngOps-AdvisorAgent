// <copyright file="Requires.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

namespace InfraService.Utilities
{
    using Validation;

    /// <summary>
    /// Requires Class, check for null.
    /// </summary>
    public static class Requires
    {
        /// <summary>
        /// Helper method to throw ArgumentNullException if a parameter is null.
        /// </summary>
        /// <typeparam name="T">Parameter of Type T.</typeparam>
        /// <param name="instance">Instance of type T.</param>
        /// <param name="paramName">Parameter Name.</param>
        /// <returns>Type Parameter T.</returns>
        /// <remarks>From https://stackoverflow.com/questions/26334478/better-way-of-checking-null-in-dependency-injection.</remarks>
        public static T IsNotNull<T>([ValidatedNotNull] T instance, string paramName)
            where T : class
        {
            if (instance is null)
            {
                // Call a method that throws instead of throwing directly. This allows this IsNotNull method to be inlined.
                // See http://www.hanselman.com/blog/ReleaseISNOTDebug64bitOptimizationsAndCMethodInliningInReleaseBuildCallStacks.aspx
                ThrowArgumentNullException(paramName);
            }

            return instance!;
        }

        /// <summary>
        /// Helper method to return false if a parameter is null.
        /// </summary>
        /// <typeparam name="T">Parameter of Type T.</typeparam>
        /// <param name="instance">Instance of type T.</param>
        /// <param name="paramName">Parameter Name.</param>
        /// <returns>Type Parameter T.</returns>
        /// <remarks>From https://stackoverflow.com/questions/26334478/better-way-of-checking-null-in-dependency-injection.</remarks>
        public static bool IsNotNullCheck<T>([ValidatedNotNull] T instance, string paramName)
            where T : class
        {
            if (instance is null)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Helper method to throw ArgumentNullException if a parameter is null or empty.
        /// </summary>
        /// <typeparam name="T">Parameter of Type T.</typeparam>
        /// <param name="instance">Instance of type T.</param>
        /// <param name="paramName">Parameter Name.</param>
        /// <returns>Type Parameter T.</returns>
        public static IEnumerable<T> IsNotNullOrEmpty<T>([ValidatedNotNull] IEnumerable<T> instance, string paramName)
            where T : class
        {
            if (instance is null || !instance.Any())
            {
                ThrowArgumentNullException(paramName);
            }

            return instance!;
        }

        /// <summary>
        /// Checks for Null or Empty.
        /// </summary>
        /// <param name="value">Parameter Value.</param>
        /// <param name="paramName">Parameter Name.</param>
        /// <returns>Returns Value if not Null Or Empty.</returns>
        public static string IsNotNullOrEmpty([ValidatedNotNull] string value, string paramName)
        {
            IsNotNull(value, paramName);
            if (value == string.Empty)
            {
                ThrowArgumentException("String cannot be empty", paramName);
            }

            return value;
        }

        /// <summary>
        /// Checks if it is Null or Empty.
        /// </summary>
        /// <typeparam name="T">Type Parameter T.</typeparam>
        /// <param name="value">Parameter Value.</param>
        /// <param name="paramName">Parameter Name.</param>
        /// <returns>List of Type T.</returns>
        public static IList<T> IsNotNullOrEmpty<T>([ValidatedNotNull] IList<T> value, string paramName)
        {
            // NOTE: the reason we are not using IEnumerable here is because using IEnumerable
            // could cause multiple enumerations when we use Any() on it.
            IsNotNull(value, paramName);
            if (!value.Any())
            {
                throw new ArgumentException("Sequence is empty", paramName);
            }

            return value;
        }

        /// <summary>
        /// Checks for Is Null Or Whitespace.
        /// </summary>
        /// <param name="value">Parameter Value.</param>
        /// <param name="paramName">Parameter Name.</param>
        /// <returns>Returns String Value of the parameter.</returns>
        public static string IsNotNullOrWhitespace([ValidatedNotNull] string value, string paramName)
        {
            IsNotNull(value, paramName);
            if (string.IsNullOrWhiteSpace(value))
            {
                ThrowArgumentException("String cannot be empty or contains only whitespace", paramName);
            }

            return value;
        }

        /// <summary>
        /// Throws Null Exception.
        /// </summary>
        /// <param name="paramName">Parameter Name.</param>
        private static void ThrowArgumentNullException(string paramName)
        {
            throw new ArgumentNullException(paramName);
        }

        /// <summary>
        /// Throws Argument Exception.
        /// </summary>
        /// <param name="message">Message for the Parameter.</param>
        /// <param name="paramName">Parameter Name.</param>
        private static void ThrowArgumentException(string message, string paramName)
        {
            throw new ArgumentException(message, paramName);
        }
    }
}
