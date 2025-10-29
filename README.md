# AI (Artificial Intelegence)
**AI** = Anything that mimics human intelgence like chess program, chatbots, recommendation engine

**Machine Learning** = AI that learn patterns from data instead of following hard-coded rules

**Deep Learning** = Machine Learning using neural network (brain like structure with layers)

## LLMs (Large Language Models)
LLMs are deep learning models trained on massive amount of text to predict the next word. Think of it like auto-complete but so good, seems like understanding. This is ChatGPT, Claude, and other AI tools we use.

## Tokens
Tokens are how AI reads text, roughly 4 charactors = 1 token<br>
"hello world" = 2 token<br>
"supercalifragilisticexpialidocious" = 8 token

Understanding the tokens, saves you money and prevents mysterious errors or LLMs halucinations.
The better we manage the tokens, the better we get the outputs.

## Context Window
Think of context window as number of pages an AI model can remember in one coversation.<br>
GPT 4: 128K Tokens (100 Pages)<br>
Claude 4: 200K Tokens (150 Pages)<br>
If you hit the limit, AI forgets a lot mid-conversation.

## Temperature Settings
Understaning the temperature settings decides how much creativity LLM model can use for the output.

temperature 0 = robotic, deterministic response (same input = same output)<br>
temperature 0.7 = balanced creativity<br>
temparature 2 = complete chaos, not sure what the response is and why

Wrong temperature destroys the response everytime.

## Prompt Engineerings
Prompt engineering is a technique to frame context, give examples and structure the request.<br>
The difference between a random user vs AI Power User, good prompt = 10x better result.<br>
A bad prompt can make GPT4 perform bad than GPT3.

## System Prompts
They are the first instruction that defines how AI should behave.<br>
"You are a helpful assistant" vs "You are brutely honest business consultants"<br>
Mastering this and you control how AI responses to everything, ignore it and AI will surpise you in (very) bad ways.

```
“Tokens” are what it reads/writes,
“Temperature” controls how it chooses words,
“System prompts” define who it is.
```

## Fine Tuning
Learn to fine tune when prompting isn't enough.<br>
You take a pre-trained model and train it further on your specific data, liking hiring a general expert and teaching them your industry.<br>
Expensieve and complex, but creates AI that thinks exactly the way you want.<br>
Only use when prompting isn't enough.

## RAG (Retrieval Augmented Generation)
RAG let's AI search your data in data in real time, like giving AI a perfect memory of your companies knowledge base.<br>
Cheaper, faster then fine tuning.<br>
Most business AI application should start here.

## APIs (Application Programming Interface)
How softwares talks to software.<br>
OpenAI APIs let's you send text and get AI responses back<br>
This moves AI from a chat interface to integrated tool<br>
Suddenly you CRM, email, website can all become AI-powered.

## Embeddings
AI converts "the cat sat on the mat" into a list of 1536 numbers<br>
Similar meaning get similar numbers, this enable AI to understand meaning no just match keywords.<br>
Thisis foundation of smart search and recommendations.

## Vector Database
Use vector database for Sementic Search.<br>
Traditional database search exact matches, vector database finds similar meaning.<br>
Search "CEO Compensation" and find "Executive Salary Packages"<br>
This helps AI find relevent information from massive datasets<br>
This powers every smart search system you have ever used.

## AI Agents
Agent frameworks let's AI browse websites, run code, send emails, use tools.<br>
They have goals and can break them down into steps<br>
This changes everything, agents don't just answer "how to book a flight", they book the flight for you.

## Multi-Model AI
Process text, image, audio and video all together.<br>
GPT4V don't just see images, it describes them<br>
Wisper converts speach to text<br>
The worlds isn't just text, multi-model AI can understand and create any type of context.

## Function calling for complext automation
Lets AI trigger your APIs, query database and send message.<br>
"book a meeting" becomes actual a calander integration.<br>
Turn AI from smart chatbot into capable digital assistant<br>
this is the difference between impresive demo vs useful tool.

## Chain of Though reasoning
Instead of jumping to answers, AI explains its thinking step-by-step, it improves accuracy on complex problems by 30-50%<br>
It's essential for any task where being wrong has consequences<br>
It helps you verify AI logic and catch errors before they matter.

## Neural Architecture
transformers = text (GPT, Claude)<br>
CNNs = image (object recognition)<br>
RNNs = sequences (time series, speech)

choose the wrong architecture and your performance will decrease<br>
understanding this helps you pick right tool for right job.

## Trnasfer Learning
**VERY IMPORTNANT TO UNDERSTAND THE AI BUSINESS**<br>
Instead of trainning form scratch (cost millions), you start with pre-trained models<br>
It's like hiring an expert and teaching them your domain<br>
Small teams can build sophisticated AI without Google sized budget<br>
this is the reason of AI development exploded in last 5 years.

## RLHF - Why Modern AI works
Understanding Reinforcement Learning from Human Feedback (RLHF) trains AI on human preferances<br>
These human rate AI response as good/bad, AI learns maximize scores <br>
This is how ChatGPT learned to be helpful instead of being accurate

This whole concepts explains why modern AI feels so much more useful than earlier versions 

## AI Safety
Understaning AI safety before deployment<br>
Content filtering, bias detection, alignment techniques <br>
These ensures AI behaves according to human values<br>
Unaligned AI can spred mis information, be manupulated, or cause harm<br>
Every production system needs built in safety guardrails

## Edge Deployment for Privacy
Models compressed to run on phones, tablets and IoT devices<br>
The data stays on device and responses are instant<br>
It enables AI in situations like poor connectivity and keeps sensitive informations from leaving your control.

## Evaluate a model
Accuracy, Precision, recall, F1-score, perplexity<br>
Human evaluation for subjective tasks <br>
AI can feel impressive but fail on edge cases<br>
A proper evalution catches problems before users catches them

## Monitoring for production
Understanding monitoring for production by tracking
- response times
- error rates
- user satisfaction
- model performance
get alerts when AI behaviour changes unexpectedly

AI models degrade overtime without maintaince
Monitoring prevent silent failures that destroy user trusts

## Custom training
collect your data, define your task, train your model

most expensive but most powerful option<br> 
when prompting, RAG, fine-tuning isn't enough

## Roadmap
- Week 1: Prompting (tokens, temperature, system prompts)
- Week 2: Data (embeddings, vectors, RAG)
- Week 3: Applications (APIs, agents, function calling)
- Week 4: Custom solutions (fine-tuning, deployment, monitoring)

