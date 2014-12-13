﻿//----------------------------------------------------------------------------------------------
//    Copyright 2014 Microsoft Corporation
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//----------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System.Net.Http;
using System.Globalization;
using System.Net.Http.Headers;
using Windows.Data.Json;
using System.Linq;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=391641

namespace TodoListClient
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// The page implements IWebAuthenticationContinuable, as it is necessary for pages containing actions that can trigger authentication
    /// </summary>
    public sealed partial class MainPage : Page, IWebAuthenticationContinuable
    {
        #region init

        //
        // The Client ID is used by the application to uniquely identify itself to Azure AD.
        // The Tenant is the name of the Azure AD tenant in which this application is registered.
        // The AAD Instance is the instance of Azure, for example public Azure or Azure China.
        // The Authority is the sign-in URL of the tenant.
        //

        // PROD
        //const string aadInstance = "https://login.windows.net/{0}";
        //const string tenant = "renxuoutlook.onmicrosoft.com";
        //const string clientId = "f5bf33e7-334f-4039-b857-6b5d8dd2bc04";
        

        // PPE
        const string aadInstance = "https://login.windows-ppe.net/{0}";
        const string tenant = "rexuaz3.ccsctp.net"; //"rexuaz4.ccsctp.net"; 
        const string clientId = "572aac4b-cac8-4dbd-9a59-a0ff16ca74e6"; //"a535a872-652a-41d7-8919-efc98d2be2d6";


        static string authority = String.Format(CultureInfo.InvariantCulture, aadInstance, tenant);

        //
        // To authenticate to the To Do list service, the client needs to know the service's App ID URI.
        // To contact the To Do list service we need it's URL as well.
        //
        const string todoListResourceId = "00000015-0000-0000-c000-000000000000";//"https://rexuaz3.ccsctp.net/TodoListService";
        const string todoListBaseAddress = "https://localhost:44321";


        private HttpClient httpClient = new HttpClient();
        private AuthenticationContext authContext = null;
        private Uri redirectURI = null;
        private string _callback = null;

#endregion
        public MainPage()
        {
            this.InitializeComponent();

            this.NavigationCacheMode = NavigationCacheMode.Required;

            //
            // Every Windows Store application has a unique URI.
            // Windows ensures that only this application will receive messages sent to this URI.
            // ADAL uses this URI as the application's redirect URI to receive OAuth responses.
            // 
            // To determine this application's redirect URI, which is necessary when registering the app
            //      in AAD, set a breakpoint on the next line, run the app, and copy the string value of the URI.
            //      This is the only purposes of this line of code, it has no functional purpose in the application.
            //
            redirectURI = Windows.Security.Authentication.Web.WebAuthenticationBroker.GetCurrentApplicationCallbackUri();

            // ADAL for Windows Phone 8.1 builds AuthenticationContext instances throuhg a factory, which performs authority validation at creation time
            // authContext = AuthenticationContext.CreateAsync(authority).GetResults();
            this.Loaded += MainPage_Loaded;
        }

        async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            authContext = await AuthenticationContext.CreateAsync(authority);
            authContext.ToString();
        }

        #region IWebAuthenticationContinuable implementation
        
        // This method is automatically invoked when the application is reactivated after an authentication interaction throuhg WebAuthenticationBroker.        
        public async void ContinueWebAuthentication(WebAuthenticationBrokerContinuationEventArgs args)
        {
            // pass the authentication interaction results to ADAL, which will conclude the token acquisition operation and invoke the callback specified in AcquireTokenAndContinue.
            await authContext.ContinueAcquireTokenAsync(args);
        }
        #endregion
        
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
           
        }

        #region Callbacks
        // Retrieve the user's To Do list.
        public async void GetTodoList(AuthenticationResult result)
        {

            if (result.Status == AuthenticationStatus.Success)
            {
                //
                // Add the access token to the Authorization Header of the call to the To Do list service, and call the service.
                //
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
                HttpResponseMessage response = await httpClient.GetAsync(todoListBaseAddress + "/api/todolist");

                if (response.IsSuccessStatusCode)
                {
                    // Read the response as a Json Array and databind to the GridView to display todo items
                    var todoArray = JsonArray.Parse(await response.Content.ReadAsStringAsync());

                    TodoList.ItemsSource = from todo in todoArray
                                           select new
                                           {
                                               Title = todo.GetObject()["Title"].GetString()
                                           };
                }
                else
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        // If the To Do list service returns access denied, clear the token cache and have the user sign-in again.
                        MessageDialog dialog = new MessageDialog("Sorry, you don't have access to the To Do Service.  Please sign-in again.");
                        await dialog.ShowAsync();
                        authContext.TokenCache.Clear();
                    }
                    else
                    {
                        MessageDialog dialog = new MessageDialog("Sorry, an error occurred accessing your To Do list.  Please try again.");
                        await dialog.ShowAsync();
                    }
                }
            }
            else
            {
                MessageDialog dialog = new MessageDialog(string.Format("If the error continues, please contact your administrator.\n\nError: {0}\n\nError Description:\n\n{1}", result.Error, result.ErrorDescription), "Sorry, an error occurred while signing you in.");
                await dialog.ShowAsync();
            }
        }

        // Post a new item to the To Do list.
        public async void AddTodo(AuthenticationResult result)
        {

            if (result.Status == AuthenticationStatus.Success)
            {
                //
                // Add the access token to the Authorization Header of the call to the To Do list service, and call the service.
                //
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
                HttpContent content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("Title", txtTodo.Text) });

                // Call the todolist web api
                var response = await httpClient.PostAsync(todoListBaseAddress + "/api/todolist", content);

                if (response.IsSuccessStatusCode)
                {
                    // Read the response as a Json Array and databind to the GridView to display todo items
                    txtTodo.Text = "";
                    GetTodoList(result);
                }
                else
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        // If the To Do list service returns access denied, clear the token cache and have the user sign-in again.
                        MessageDialog dialog = new MessageDialog("Sorry, you don't have access to the To Do Service.  Please sign-in again.");
                        await dialog.ShowAsync();
                        authContext.TokenCache.Clear();
                    }
                    else
                    {
                        MessageDialog dialog = new MessageDialog("Sorry, an error occurred accessing your To Do list.  Please try again.");
                        await dialog.ShowAsync();
                    }
                }
            }
            else
            {
                MessageDialog dialog = new MessageDialog(string.Format("If the error continues, please contact your administrator.\n\nError: {0}\n\nError Description:\n\n{1}", result.Error, result.ErrorDescription), "Sorry, an error occurred while signing you in.");
                await dialog.ShowAsync();
            }
        }
        #endregion

        #region AppBar buttons

        // clear the token cache
        private void RemoveAppBarButton_Click(object sender, RoutedEventArgs e)
        {
             // Clear session state from the token cache.
            authContext.TokenCache.Clear();

            // Reset UI elements
            TodoList.ItemsSource = null;
            //TodoText.Text = "";
        }

        // fetch the user's To Do list from the service. If no tokens are present in the cache, trigger the authentication experience before performing the call
        private async void RefreshAppBarButton_Click(object sender, RoutedEventArgs e)
        {
            // Try to get a token without triggering any user prompt. 
            // ADAL will check whether the requested token is in the cache or can be obtained without user itneraction (e.g. via a refresh token).
            AuthenticationResult result = await authContext.AcquireTokenSilentAsync(todoListResourceId, clientId);
            if (result != null && result.Status == AuthenticationStatus.Success)
            {
                // A token was successfully retrieved. Get the To Do list for the current user
                GetTodoList(result);
            }
            else
            {
                _callback = "get";
                login_popup.IsOpen = true;
            }
        }
        #endregion

        // Post a new item to the To Do list. If no tokens are present in the cache, trigger the authentication experience before performing the call
        private async void btnAddTodo_Click(object sender, RoutedEventArgs e)
        {
            // Try to get a token without triggering any user prompt. 
            // ADAL will check whether the requested token is in the cache or can be obtained without user itneraction (e.g. via a refresh token).
            AuthenticationResult result = await authContext.AcquireTokenSilentAsync(todoListResourceId, clientId);
            if (result != null && result.Status == AuthenticationStatus.Success)
            {
                // A token was successfully retrieved. Post the new To Do item
                AddTodo(result);
            }
            else
            {
                _callback = "add";
                login_popup.IsOpen = true;
            }
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            string idp = (string)button.DataContext;
            login_popup.IsOpen = false;

            // Acquiring a token without user interaction was not possible. 
            // Trigger an authentication experience and specify that once a token has been obtained the AddTodo method should be called
            if (_callback == null)
                return;
            if (_callback == "add")
            {
                authContext.AcquireTokenAndContinue(todoListResourceId, clientId, redirectURI, UserIdentifier.AnyUser, "domain_hint=" + idp, AddTodo);
                _callback = null;    
            }
            else if (_callback == "get")
            {
                authContext.AcquireTokenAndContinue(todoListResourceId, clientId, redirectURI, UserIdentifier.AnyUser, "domain_hint=" + idp, GetTodoList);
                _callback = null;
            }
        }

        private void Login_Close(object sender, RoutedEventArgs e)
        {
            if (login_popup.IsOpen)
            {
                login_popup.IsOpen = false;
                _callback = null;
            }
        }
    }
}
