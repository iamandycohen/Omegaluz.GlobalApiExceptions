using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Filters;

namespace Omegaluz.GlobalApiExceptions
{
    public class GlobalApiExceptionFilterAttribute : ExceptionFilterAttribute
    {

        const string ERROR_CODE_KEY = "ErrorCode";
        const string ERROR_REFERENCE_KEY = "ErrorReference";

        List<GlobalApiExceptionDefinition> exceptionHandlers;
        bool catchUnfilteredExceptions;

        public GlobalApiExceptionFilterAttribute(
            List<GlobalApiExceptionDefinition> exceptionHandlers = null, bool catchUnfilteredExceptions = false)
        {
            this.exceptionHandlers = exceptionHandlers ?? new List<GlobalApiExceptionDefinition>();
            this.catchUnfilteredExceptions = catchUnfilteredExceptions;
        }

        public override void OnException(HttpActionExecutedContext actionExecutedContext)
        {
            var exception = actionExecutedContext.Exception;
            GlobalApiExceptionDefinition globalExceptionDefinition = null;
            HttpStatusCode statusCode = HttpStatusCode.InternalServerError;

            if (LookupException(actionExecutedContext.Exception, out globalExceptionDefinition) || catchUnfilteredExceptions)
            {
                // set the friendly message
                string friendlyMessage = globalExceptionDefinition != null ? globalExceptionDefinition.FriendlyMessage(exception) : exception.Message;

                // create the friendly http error
                var friendlyHttpError = new HttpError(friendlyMessage);

                // if we found a globalExceptionDefinition then set properties of our friendly httpError object accordingly
                if (globalExceptionDefinition != null)
                {

                    // set the status code
                    statusCode = globalExceptionDefinition.StatusCode;

                    // add optional error code
                    if (!string.IsNullOrEmpty(globalExceptionDefinition.ErrorCode))
                    {
                        friendlyHttpError[ERROR_CODE_KEY] = globalExceptionDefinition.ErrorCode;
                    }

                    // add optional error reference
                    if (!string.IsNullOrEmpty(globalExceptionDefinition.ErrorReference))
                    {
                        friendlyHttpError[ERROR_REFERENCE_KEY] = globalExceptionDefinition.ErrorReference;
                    }

                }

                // set the response to our friendly http error
                actionExecutedContext.Response = actionExecutedContext.Request.CreateErrorResponse(statusCode, friendlyHttpError);

            }

            // flow through to the base
            base.OnException(actionExecutedContext);
        }

        private bool LookupException(Exception exception, out GlobalApiExceptionDefinition exceptionMatch)
        {
            exceptionMatch = null;

            var possibleMatches = exceptionHandlers.Where(e => e.ExceptionType == exception.GetType());
            foreach (var possibleMatch in possibleMatches)
            {
                if (possibleMatch.Handle == null || possibleMatch.Handle(exception))
                {
                    exceptionMatch = possibleMatch;

                    return true;
                }
            }

            return false;
        }

    }

    public class GlobalApiExceptionDefinition
    {

        const string ARGUMENT_NULL_EXCEPTION_FMT = "Argument '{0}' cannot be null.";
        const string ARGUMENT_MUST_INHERIT_FROM_FMT = "Type must inherit from {0}.";

        public Type ExceptionType { get; private set; }
        public Func<Exception, string> FriendlyMessage { get; private set; }

        public Func<Exception, bool> Handle { get; set; }
        public HttpStatusCode StatusCode { get; set; }

        public string ErrorCode { get; set; }
        public string ErrorReference { get; set; }

        public GlobalApiExceptionDefinition(Type exceptionType, string friendlyMessage = null, HttpStatusCode statusCode = HttpStatusCode.InternalServerError) :
            this(exceptionType, (ex) => friendlyMessage ?? ex.Message, statusCode) { }

        public GlobalApiExceptionDefinition(Type exceptionType, Func<Exception, string> friendlyMessage, HttpStatusCode statusCode = HttpStatusCode.InternalServerError)
        {

            AssertParameterIsNotNull(friendlyMessage, "friendlyMessage");
            AssertParameterIsNotNull(exceptionType, "exceptionType");
            AssertParameterInheritsFrom(exceptionType, typeof(Exception), "exceptionType");

            ExceptionType = exceptionType;
            FriendlyMessage = friendlyMessage;
            StatusCode = statusCode;
        }

        #region "Argument Assertions"

        private static void AssertParameterInheritsFrom(Type type, Type inheritedType, string name)
        {
            if (!type.IsSubclassOf(inheritedType))
            {
                throw new ArgumentException(string.Format(ARGUMENT_MUST_INHERIT_FROM_FMT, inheritedType.Name), name);
            }
        }

        private static void AssertParameterIsNotNull(object parameter, string name)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(name, string.Format(ARGUMENT_NULL_EXCEPTION_FMT, name));
            }
        }

        #endregion

    }
}
