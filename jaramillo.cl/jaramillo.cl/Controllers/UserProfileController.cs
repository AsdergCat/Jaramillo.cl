﻿using jaramillo.cl.APICallers;
using jaramillo.cl.Common;
using jaramillo.cl.Models.APIModels;
using jaramillo.cl.Models.ViewModels;
using Microsoft.AspNet.Identity;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Web.Mvc;

namespace jaramillo.cl.Controllers
{
    [Authorize]
    [RoutePrefix("perfil")]
    public class UserProfileController : BaseController
    {
        readonly UsuariosCaller UC = new UsuariosCaller();
        readonly BookingCaller BC = new BookingCaller();
        readonly SalesCaller SC = new SalesCaller();

        public string currentUserId = "";

        private const string updateRoute = "actualizar-datos";
        private const string ChangePasswordUrl = "cambiar-contraseña";

        /* ---------------------------------------------------------------- */
        /* PROFILE */
        /* ---------------------------------------------------------------- */
        /// <summary>
        /// GET |   Returns a View with the Profile information fo the current logged Used
        /// </summary>
        [HttpGet]
        [Route]
        
        public ActionResult Profile()
        {
            var userId = User.Identity.GetUserId();
            if (userId == null) return Error_FailedRequest();

            Usuario usuario;

            try
            {
                usuario = UC.GetUser(userId);
                if (usuario == null) return Error_FailedRequest();

                var userTypeLst = UC.GetAllTypes().ToList();
                if (userTypeLst == null) return Error_FailedRequest();

                var userStatusLst = UC.GetAllStatus().ToList();
                if (userStatusLst == null) return Error_FailedRequest();

                usuario = UC.ProcessUser(usuario, userTypeLst, userStatusLst);
            }
            catch (Exception e)
            {
                ErrorWriter.ExceptionError(e);
                return Error_CustomError(e.Message);
            }

            return View(usuario);
        }


        /* ---------------------------------------------------------------- */
        /* UPDATE PROFILE */
        /* ---------------------------------------------------------------- */
        /// <summary>
        /// GET |   Returns a form to update some profile information
        /// </summary>
        [HttpGet]
        [Route(updateRoute)]
        public ActionResult UpdateProfile()
        {
            Usuario usuario;
            List<UserType> userTypeLst;
            List<UserStatus> userStatusLst;

            try
            {
                var userId = User.Identity.GetUserId();
                if (userId == null) return Error_FailedRequest();

                usuario = UC.GetUser(userId);
                if (usuario == null) return Error_FailedRequest();

                userTypeLst = UC.GetAllTypes().ToList();
                if (userTypeLst == null) return Error_FailedRequest();

                userStatusLst = UC.GetAllStatus().ToList();
                if (userStatusLst == null) return Error_FailedRequest();
            }
            catch (Exception e)
            {
                ErrorWriter.ExceptionError(e);
                return Error_CustomError(e.Message);
            }

            ViewBag.userTypeLst = new SelectList(userTypeLst, "user_type_id", "name", usuario.user_type_id);
            ViewBag.userStatusLst = new SelectList(userStatusLst, "status_id", "status", usuario.status_id);

            return View(usuario);
        }

        /// <summary>
        /// POST    |   API call to update the profile data in the DB. 
        /// </summary>
        /// <param name="newUser">Model with the new User Data</param>
        [HttpPost]
        [Route(updateRoute)]
        public ActionResult UpdateProfile(Usuario newUser)
        {
            if (newUser == null) return Error_InvalidUrl();

            try
            {
                Usuario oldUser = UC.GetUser(newUser.appuser_id);
                if (oldUser == null) return Error_FailedRequest();

                // To make sure only the editable data is modified
                oldUser.name = newUser.name;
                oldUser.last_names = newUser.last_names;
                oldUser.rut = newUser.rut;
                oldUser.adress = newUser.adress;
                oldUser.phone = newUser.phone;
                oldUser.birthday = newUser.birthday;

                var res = UC.UpdateUser(oldUser);
                if (!res)
                {
                    Error_FailedRequest();
                    return RedirectToAction("UpdateProfile", new { userId = newUser.appuser_id });
                }
            }
            catch (Exception e)
            {
                ErrorWriter.ExceptionError(e);
                Error_CustomError(e.Message);
                return RedirectToAction("UpdateProfile", new { userId = newUser.appuser_id });
            }

            string successMsg = "Su perfil fue actualizado con éxito";
            SetSuccessMsg(successMsg);

            return RedirectToAction("Profile");
        }


        /* ---------------------------------------------------------------- */
        /* CHANGE PASSWORD */
        /* ---------------------------------------------------------------- */
        /// <summary>
        /// GET |   Returns a form to change the current logged User password
        /// </summary>
        [HttpGet]
        [Route(ChangePasswordUrl)]
        public ActionResult ChangePassword()
        {
            // Get current userId
            var userId = User.Identity.GetUserId();
            if (userId == null) return Error_FailedRequest();

            return View();
        }

        /// <summary>
        /// POST    |   API call to update the current logged User password in the DB. 
        /// </summary>
        /// <param name="newPsw">New Password</param>
        /// <param name="newPsw2">Repeat the new Password</param>
        /// <param name="oldPsw">Old Password to validate</param>
        /// <param name="oldPsw2">Repeat the old Password</param>
        [HttpPost]
        [Route(ChangePasswordUrl)]
        public ActionResult ChangePassword(string newPsw, string newPsw2, string oldPsw, string oldPsw2)
        {
            if (string.IsNullOrEmpty(newPsw) || string.IsNullOrEmpty(newPsw2) ||
                string.IsNullOrEmpty(oldPsw) || string.IsNullOrEmpty(oldPsw2)) 
                    return Error_InvalidForm(false);

            if (!oldPsw.Equals(oldPsw2) || !newPsw.Equals(newPsw2)) 
                return Error_CustomError("Las contraseñas ingresadas no coinciden", false);

            try
            {
                var res = UC.UpdatePassword(newPsw, oldPsw);
                if (!res) return Error_FailedRequest();
            }
            catch (Exception e)
            {
                ErrorWriter.ExceptionError(e);
                return Error_CustomError(e.Message, false);
            }

            string successMsg = "Su contraseña fue cambiada";
            SetSuccessMsg(successMsg);

            return RedirectToAction("Profile");
        }


        /* ---------------------------------------------------------------- */
        /* SERVICE LIST */
        /* ---------------------------------------------------------------- */
        /// <summary>
        /// GET |   Returns a View with all the Services contracted by the current Used
        /// </summary>
        [Authorize(Roles = "CLI")]
        [HttpGet]
        public ActionResult ServList()
        {
            List<UserServVM> servList = new List<UserServVM>();

            try
            {
                var userId = User.Identity.GetUserId();
                if (userId == null) return Error_FailedRequest();

                string n = string.Empty;

                var saleList = SC.GetAllSales(n, "PAG", id_appuser: userId);
                if (saleList == null) return Error_FailedRequest();

                foreach (var sale in saleList)
                {
                    sale.saleItems = SC.GetSaleItems(sale.sale_id).ToList();

                    foreach (var item in sale.saleItems)
                    {
                        if(item.serv != null)
                        {
                            var newServ = new UserServVM()
                            {
                                serv = item.serv,
                                date = sale.created_at,
                                total = item.total
                            };
                            servList.Add(newServ);
                        }
                    }
                }

            }
            catch (Exception e)
            {
                ErrorWriter.ExceptionError(e);
                return Error_CustomError(e.Message);
            }

            return View(servList);
        }


        /* ---------------------------------------------------------------- */
        /* CHECK BOOKING */
        /* ---------------------------------------------------------------- */
        /// <summary>
        /// GET |   Returns a View with all the Bookings made by the current Used
        /// </summary>
        [Authorize(Roles = "CLI")]
        [HttpGet]
        public ActionResult CheckBookings()
        {
            List<BookingVM> bookList;

            try
            {
                var userId = User.Identity.GetUserId();
                if (userId == null) return Error_FailedRequest();

                bookList = BC.GetAllBookings("ACT" , userId).ToList();
                if (bookList == null) return Error_FailedRequest();
            }
            catch (Exception e)
            {
                ErrorWriter.ExceptionError(e);
                return Error_CustomError(e.Message);
            }

            return View(bookList);
        }


        /* ---------------------------------------------------------------- */
        /* RESCHEDULE DETAILS */
        /* ---------------------------------------------------------------- */

        /// <summary>
        /// POST  |  API call to reschedule the data of a Booking updating the data in the DB
        /// </summary>
        [HttpPost]
        public ActionResult RescheduleBook(RescheduleBookVM model)
        {
            if (model == null) return Error_InvalidUrl();
            var newBook = model.booking;
            string bookId = newBook.booking_id;

            try
            {
                var isAvailable = CheckBookAvailability(newBook);
                if (isAvailable == null)
                {
                    Error_FailedRequest();
                    return RedirectToAction("CheckBookings", new { bookId });
                }
                else if (isAvailable == false)
                {
                    SetErrorMsg("Ya hay una hora agendada para esa hora o hay conflicto con el horario de la tienda, por favor seleccione una diferente");
                    return RedirectToAction("CheckBookings", new { bookId });
                }

                Booking apiNewBook = newBook;

                var res = BC.UpdateBooking(apiNewBook);

                if (!res)
                {
                    Error_FailedRequest();
                    return RedirectToAction("CheckBookings");
                }
            }
            catch (Exception e)
            {
                ErrorWriter.ExceptionError(e);
                Error_CustomError(e.Message);
                return RedirectToAction("CheckBookings");
            }

            string successMsg = "La Reserva fue reagendada con éxito";
            SetSuccessMsg(successMsg);

            return RedirectToAction("CheckBookings");
        }

        /// <summary>
        /// Returns the HTML of the modal to reschedule a Booking for an specific Booking
        /// </summary>
        /// <param name="bookId">Id of the Booking to Reschedule</param>
        public string GetRescheduleBookModalHtml(string bookId)
        {
            if (string.IsNullOrEmpty(bookId))
            {
                ErrorWriter.InvalidArgumentsError();
                return Resources.Messages.Error_SolicitudFallida;
            }

            string html;

            try
            {
                var model = new RescheduleBookVM();

                var booking = BC.GetBook(bookId);
                model.booking = booking;

                var otherBookList = BC.GetAllBookings("ACT", serv_id: booking.serv_id).ToList();
                model.otherBookList = otherBookList.Where(x => x.start_date_hour > DateTime.Now).ToList();

                var restList = BC.GetAllBookRest(booking.serv_id).ToList();
                model.restList = restList.Where(x => x.start_date_hour > DateTime.Now).ToList();

                html = PartialView("Partial/_rescheduleBookModal", model).RenderToString();
            }
            catch (Exception e)
            {
                ErrorWriter.ExceptionError(e);
                return Resources.Messages.Error_SolicitudFallida;
            }

            return html;
        }

        /// <summary>
        /// Return the HTML of the receipt of a Booking. Is expected to be put inside a <iframe>
        /// </summary>
        /// <param name="bookId">Id of the Booking</param>
        public string GetBookingReceipt(string bookId)
        {
            if (string.IsNullOrEmpty(bookId))
            {
                ErrorWriter.InvalidArgumentsError();
                return Resources.Messages.Error_SolicitudFallida;
            }

            string html;

            try
            {
                var item = BC.GetBook(bookId);

                html = PartialView("Partial/_bookingReceipt", item).RenderToString();
            }
            catch (Exception e)
            {
                ErrorWriter.ExceptionError(e);
                return Resources.Messages.Error_SolicitudFallida;
            }

            return html;
        }


        /* ---------------------------------------------------------------- */
        /* CANCEL BOOKING */
        /* ---------------------------------------------------------------- */

        /// <summary>
        /// POST  |  API call to update the Status of a Booking to Canceled
        /// </summary>
        /// <param name="bookId"> Id of the Booking to cancel </param>
        [HttpGet]
        public ActionResult CancelBook(string bookId)
        {
            if (string.IsNullOrEmpty(bookId)) return Error_InvalidForm();

            try
            {
                var res = BC.ChangeBookStatus(bookId, "CAN");
                if (!res) return Error_FailedRequest();
            }
            catch (Exception e)
            {
                ErrorWriter.ExceptionError(e);
                return Error_CustomError(e.Message);
            }

            string successMsg = "La Reserva fue cancelada";
            SetSuccessMsg(successMsg);

            string referer = GetRefererForError(Request);
            return Redirect(referer);
        }



        /* ---------------------------------------------------------------- */
        /* HELPERS */
        /* ---------------------------------------------------------------- */

        /// <summary>
        /// Revisa la disponibilidad para una reserva, comparando si hay conflicto con el horario de la tienda, 
        /// otras reservas o restricciones de horario
        /// </summary>
        /// <param name="book"> Modelo con la reserva a revisar </param>
        [NonAction]
        public bool? CheckBookAvailability(BookingVM book)
        {
            if (book == null || book.start_date_hour == null || book.end_date_hour == null) return null;

            Debug.WriteLine($" ---------------- CHECKING BOOKING AVAILABILITY ---------------- ");

            try
            {
                DateTime start = book.start_date_hour ?? default;
                DateTime end = book.end_date_hour ?? default;
                if (start == default || end == default) return null;

                var servId = book.serv_id;

                var bookDate = start.Date;

                var startTime = start.TimeOfDay;
                var endTime = end.TimeOfDay;

                Debug.WriteLine("----------------------------------------------------------------");
                Debug.WriteLine($"SERV : {book.servName}");
                Debug.WriteLine($"CHEDULE : {book.schedule}");
                Debug.WriteLine("----------------------------------------------------------------");

                //!? GET STORE SCHEDULE
                Debug.WriteLine($" ---------------- CHECKING STORE SCHEDULE ---------------- ");
                var storeSche = BC.GetStoreSche();
                if (storeSche == null) return null;

                // Extraer la hora de apertura y cierre de la tienda
                var scheSt = storeSche.start_hour.TimeOfDay;
                var scheEn = storeSche.end_hour.TimeOfDay;
                // Extraer el horario de almuerzo de la tienda
                var scheLunchSt = storeSche.start_lunch_hour.TimeOfDay;
                var scheLunchEn = storeSche.end_lunch_hour.TimeOfDay;

                Debug.WriteLine($"SCHEDULE : {scheSt} - {scheLunchSt} / {scheLunchEn} - {scheEn}");

                // Check por la apertura y cierre de la tienda
                bool checkMorning = startTime >= scheSt && endTime > scheSt;
                bool checkEvening = startTime < scheEn && endTime <= scheEn;
                // Check la hora de almuerzo como una restricción
                bool checkLunch = CheckTimeConflict(startTime, endTime, scheLunchSt, scheLunchEn);

                // Si cualquiera fallo, conflicto
                if (!checkMorning || !checkEvening || !checkLunch)
                {
                    Debug.WriteLine("> CONFLICT FOUND;");
                    return false;
                }


                Debug.WriteLine($" ---------------- CHECKING RESTRICTIONS ---------------- ");
                //!? GET RESTRICTIONS
                var restList = BC.GetAllBookRest(servId).ToList();
                if (restList == null) return null;

                // Saca solo las reservas que son para el mismo día
                restList = restList.Where(x =>
                    x.start_date_hour?.Date == bookDate)
                    .ToList();

                foreach (var rest in restList)
                {
                    DateTime chStart = rest.start_date_hour ?? default;
                    DateTime chEnd = rest.end_date_hour ?? default;
                    if (chStart == default || chEnd == default) return null;

                    Debug.WriteLine($"TIME : {chStart.Hour} - {chEnd.Hour}");

                    var check = CheckTimeConflict(start, end, chStart, chEnd);

                    if (!check)
                    {
                        Debug.WriteLine("> CONFLICT FOUND;");
                        return false;
                    }
                }



                Debug.WriteLine($" ---------------- CHECKING OTHER BOOKINGS ---------------- ");
                //!? GET OTHERS BOOKING FROM THIS SERV
                var otherBookList = BC.GetAllBookings(status_booking_id: "ACT", serv_id: servId);
                if (otherBookList == null) return null;

                // Saca solo las reservas que son para el mismo día
                // Recordar sacar a book de la lista
                otherBookList = otherBookList.Where(x =>
                    x.start_date_hour?.Date == bookDate &&
                    !x.booking_id.Equals(book.booking_id))
                    .ToList();

                // Revisar si hay conflicto con otras reservas
                foreach (var bk in otherBookList)
                {
                    DateTime chStart = bk.start_date_hour ?? default;
                    DateTime chEnd = bk.end_date_hour ?? default;
                    if (chStart == default || chEnd == default) return null;

                    Debug.WriteLine($"TIME : {chStart.Hour} - {chEnd.Hour}");

                    var check = CheckTimeConflict(start, end, chStart, chEnd);

                    if (!check)
                    {
                        Debug.WriteLine("> CONFLICT FOUND;");
                        return false;
                    }
                }

                // Si no hay conflicto, return true
                return true;
            }
            catch (Exception e)
            {
                ErrorWriter.ExceptionError(e);
                return null;
            }
        }

        /// <summary>
        /// Revisa si dos lapsus de tiempo se solapan
        /// </summary>
        /// <param name="start"> Comienzo del primer lapsus de tiempo </param>
        /// <param name="end"> Termino del primer lapsus de tiempo </param>
        /// <param name="chStart"> Comienzo del segundo lapsus de tiempo </param>
        /// <param name="chFinish"> Termino del segundo lapsus de tiempo </param>
        [NonAction]
        private bool CheckTimeConflict(TimeSpan start, TimeSpan end, TimeSpan chStart, TimeSpan chFinish)
        {
            int st_st = TimeSpan.Compare(start, chStart);
            int st_en = TimeSpan.Compare(start, chFinish);
            int en_st = TimeSpan.Compare(end, chFinish);
            int en_en = TimeSpan.Compare(end, chStart);

            var lst = new List<int>() { st_st, st_en, en_st, en_en };

            var checkMinus = lst.All(x => x <= 0);
            var checkPlus = lst.All(x => x >= 0);

            if (checkMinus || checkPlus) return true;
            else return false;
        }

        /// <summary>
        /// Revisa si dos lapsus de tiempo se solapan
        /// </summary>
        /// <param name="start"> Comienzo del primer lapsus de tiempo </param>
        /// <param name="end"> Termino del primer lapsus de tiempo </param>
        /// <param name="chStart"> Comienzo del segundo lapsus de tiempo </param>
        /// <param name="chFinish"> Termino del segundo lapsus de tiempo </param>
        [NonAction]
        private bool CheckTimeConflict(DateTime start, DateTime end, DateTime chStart, DateTime chFinish)
        {
            int st_st = DateTime.Compare(start, chStart);
            int st_en = DateTime.Compare(start, chFinish);
            int en_st = DateTime.Compare(end, chFinish);
            int en_en = DateTime.Compare(end, chStart);

            var lst = new List<int>() { st_st, st_en, en_st, en_en };

            var checkMinus = lst.All(x => x <= 0);
            var checkPlus = lst.All(x => x >= 0);

            if (checkMinus || checkPlus) return true;
            else return false;
        }

    }
}