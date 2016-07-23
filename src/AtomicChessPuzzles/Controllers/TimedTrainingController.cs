using AtomicChessPuzzles.DbRepositories;
using AtomicChessPuzzles.MemoryRepositories;
using AtomicChessPuzzles.Models;
using AtomicChessPuzzles.Services;
using Microsoft.AspNet.Mvc;
using Microsoft.AspNet.Http;
using System;

namespace AtomicChessPuzzles.Controllers
{
    public class TimedTrainingController : Controller
    {
        ITimedTrainingScoreRepository timedTrainingRepository;
        IPositionRepository positionRepository;
        ITimedTrainingSessionRepository timedTrainingSessionRepository;
        ITimedTrainingScoreRepository timedTrainingScoreRepository;
        IMoveCollectionTransformer moveCollectionTransformer;

        public TimedTrainingController(ITimedTrainingScoreRepository _timedTrainingRepository, IPositionRepository _positionRepository, ITimedTrainingSessionRepository _timedTrainingSessionRepository,
                                       ITimedTrainingScoreRepository _timedTrainingScoreRepository, IMoveCollectionTransformer _moveCollectionTransformer)
        {
            timedTrainingRepository = _timedTrainingRepository;
            positionRepository = _positionRepository;
            timedTrainingSessionRepository = _timedTrainingSessionRepository;
            timedTrainingScoreRepository = _timedTrainingScoreRepository;
            moveCollectionTransformer = _moveCollectionTransformer;
        }
        [HttpGet]
        [Route("/Puzzle/Train-Timed/Mate-In-One")]
        public IActionResult TimedMateInOne()
        {
            return View();
        }

        [HttpPost]
        [Route("/Puzzle/Train-Timed/Mate-In-One/Start")]
        public IActionResult StartMateInOneTraining()
        {
            string sessionId = Guid.NewGuid().ToString();
            DateTime startTime = DateTime.UtcNow;
            DateTime endTime = startTime + new TimeSpan(0, 1, 0);
            TimedTrainingSession session = new TimedTrainingSession(sessionId, startTime, endTime,
                                        (HttpContext.Session.GetString("userid") ?? "").ToLower(), "mateInOne");
            timedTrainingSessionRepository.Add(session);
            TrainingPosition randomPosition = positionRepository.GetRandomMateInOne();
            session.SetPosition(randomPosition);
            return Json(new { success = true, sessionId = sessionId, seconds = 60, fen = randomPosition.FEN, color = session.AssociatedGame.WhoseTurn.ToString().ToLowerInvariant(),
                              dests = moveCollectionTransformer.GetChessgroundDestsForMoveCollection(session.AssociatedGame.GetValidMoves(session.AssociatedGame.WhoseTurn)) });
        }

        [HttpPost]
        [Route("/Puzzle/Train-Timed/Mate-In-One/VerifyAndGetNext")]
        public IActionResult MateInOneVerifyAndGetNext(string sessionId, string origin, string destination)
        {
            TimedTrainingSession session = timedTrainingSessionRepository.Get(sessionId);
            if (session == null)
            {
                return Json(new { success = false, error = "Training session ID not found." });
            }
            if (session.Ended)
            {
                if (!session.RecordedInDb && !string.IsNullOrEmpty(session.Score.Owner))
                {
                    timedTrainingScoreRepository.Add(session.Score);
                    session.RecordedInDb = true;
                }
                return Json(new { success = true, ended = true });
            }
            bool correctMove = session.VerifyMove(origin, destination);
            TrainingPosition randomPosition = positionRepository.GetRandomMateInOne();
            session.SetPosition(randomPosition);
            return Json(new { success = true, fen = randomPosition.FEN, color = session.AssociatedGame.WhoseTurn.ToString().ToLowerInvariant(),
                              dests = moveCollectionTransformer.GetChessgroundDestsForMoveCollection(session.AssociatedGame.GetValidMoves(session.AssociatedGame.WhoseTurn)),
                              correct = correctMove });
        }

        [HttpPost]
        [Route("/Puzzle/Train-Timed/Mate-In-One/AcknowledgeEnd")]
        public IActionResult AcknowledgeEnd(string sessionId)
        {
            TimedTrainingSession session = timedTrainingSessionRepository.Get(sessionId);
            if (session == null)
            {
                return Json(new { success = false, error = "Training session ID not found." });
            }
            if (!session.RecordedInDb && !string.IsNullOrEmpty(session.Score.Owner))
            {
                timedTrainingScoreRepository.Add(session.Score);
                session.RecordedInDb = true;
            }
            double score = session.Score.Score;
            timedTrainingSessionRepository.Remove(session.SessionID);
            return Json(new { success = true, score = score });
        }
    }
}